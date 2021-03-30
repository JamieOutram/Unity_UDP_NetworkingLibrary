using System;
using System.Threading;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Diagnostics;
namespace UnityNetworkingLibrary
{
    using ExceptionExtensions;
    using Utils;
    using Messages;
    using Packets;
    using System.Threading.Tasks;

    //Mangages ordering and priority of packets to send
    //Options: unreliable, reliable and blocking data in packets
    //  unreliable packets are completely unreliable data, sent in one burst and forgotten
    //  reliable packets contain at least one reliable message, receive ack before forgotten. resent if no ack recieved.
    //      -various tiers for this which vary burst size and timeout. (larger bursts and timeouts for time critical flags)
    //  blocking packets behave like TCP, only communicating that packet till received ack (for things like end of game)
    public class PacketManager //TODO: decide split into client and server variants/one packet manager per connection;
    {
        public const int _maxPacketSizeBytes = 1024;
        public const int _maxPacketSizeBits = 8 * _maxPacketSizeBytes;
        public const int _maxPacketDataBytes = _maxPacketSizeBytes - Packet.headerSize;
        public const int _messageQueueSize = 200;
        public const int _packetQueueSize = 100; //At Least long enough to accomodate all encoded acks
        public const int _awaitingAckBufferSize = Packet.ackedBitsLength; //At Least long enough to accomodate all encoded acks & disconnect/resync
        public const int _receiveBufferSize = Packet.ackedBitsLength; //Number of reliable packets buffered before dropping. Should be a factor of 2^16 (ushort) due to % overflow referencing. 
        public const int _receiveQueueSize = Packet.ackedBitsLength; //Number of unreliable packets buffered before dropping

        //Define constants 
        //TODO: burst length and interval/timeout could be made dynamic based on connection or completely user defined
        const int _maxMissedPackets = 400; //The maximum number of missed packets before complete resync required (possible disconnect) 
        const int _unreliableBurstLength = 3; //Number of packets data is included in when sent
        const int _reliableBurstLength = 3; // " for reliable data
        const int _reliableTriggerBurstLength = 10; // " for reliable trigger data
        TimeSpan _reliableInterval = TimeSpan.FromMilliseconds(100); //timeout after reliable burst before assuming not received in ms NOTE: should be rarely triggered thanks to ackedbits


        volatile UInt16 currentPacketID = 0; //value incremented and assigned to each packet in the send queue

        //------Send buffers--------
        IndexableQueue<Message> messageQueue; //Messages to be sent are added to this queue acording to priority.
        IndexableQueue<Packet> packetQueue; //prepared packets waiting to be sent, fairly short queue which allows pushing of unacknowledged packets to front 
        IdBuffer<Packet> awaitingAckBuffer; //buffer storing sent reliable messages awaiting acknowledgement.

        //------Shared buffers--------
        IndexableQueue<(ushort, AckBitArray)> ackQueue; //Id and encoded ack info to attach to sent packets

        //------Receive buffers--------
        IdBuffer<(Packet.Header, byte[])> receiveBuffer; //Received reliable packets are stored and ordered in this buffer
        IndexableQueue<(Packet.Header, byte[])> receiveQueue; //Received unreliable packets are stored here for processing
        AckBuffer receivedPacketAcks; //Acknowledgment state mask for received packets used to create ack encoding attached to sent packets
        AckBuffer sentPacketAcks; //Acknowledgment state mask for sent packets that have been acknowledged by receiver

        bool isConnected = false;

        //Thread Locks
        object messageQueueLock = new object(); //Message queue can be changed at any time by public accessor, other queues only effected by manager thread
        object ackInfoLock = new object(); //ack info could be updated as it is being added to a packet for sending;
        object receiveBufferLock = new object(); //Messages received could be added at the same time as the manager attempts to process them;
        object receiveQueueLock = new object();
        object packetQueueLock = new object(); //Messages can be sent by multiple threads

        //Main Manager thread
        Thread mainThread;

        //Packet Decoding thread
        Thread decodeThread;

        static RNGCryptoServiceProvider random = new RNGCryptoServiceProvider(); //Secure random function
        ulong privateSalt;
        ulong salt;
        ushort lastSentAckId;
        AckBitArray lastSentAckInfo;

        UDPSocket socket;

        PacketManager(UDPSocket sock)
        {
            //define arrays
            ackQueue = new IndexableQueue<(ushort, AckBitArray)>(_packetQueueSize);
            messageQueue = new IndexableQueue<Message>(_messageQueueSize);
            packetQueue = new IndexableQueue<Packet>(_packetQueueSize);
            awaitingAckBuffer = new IdBuffer<Packet>(_awaitingAckBufferSize);
            receiveBuffer = new IdBuffer<(Packet.Header, byte[])>(_receiveBufferSize);
            receiveQueue = new IndexableQueue<(Packet.Header, byte[])>(_receiveQueueSize);
            receivedPacketAcks = new AckBuffer(_receiveBufferSize);
            sentPacketAcks = new AckBuffer(_receiveBufferSize);
            socket = sock;
            Initialize();
        }

        //Resets all manager properties to defaults
        public void Clean()
        {
            ackQueue.Clear();
            latestPacketIdReceived = 0;
            currentPacketID = 0;
            messageQueue.Clear();
            packetQueue.Clear();
            awaitingAckBuffer.Clear();
            receiveBuffer.Clear();
            privateSalt = GetNewSalt(); //Generate new private salt
        }

        //TODO: Packet Manager should be its own thread
        //Creates a new thread for the manager, returns refernce to the new thread.
        void Initialize()
        {
            privateSalt = GetNewSalt();
            socket.OnReceived += OnReceive;
            mainThread = new Thread(() => ThreadLoop(ManagerLoop, 60));
            decodeThread = new Thread(() => ThreadLoop(ReceiveLoop, 120));
        }

        //Cleans and starts the manager on an asynchronous thread
        public void Start()
        {
            //If the thread is already running do not start
            if(mainThread.IsAlive)
                throw new ThreadStateException();
            if(decodeThread.IsAlive)
                throw new ThreadStateException();

            Clean();
            mainThread.Start();
            decodeThread.Start();
        }

        //Stops and joins the manager thread
        public void Stop()
        {
            //If the thread is not running, throw exception
            if (!mainThread.IsAlive||!decodeThread.IsAlive)
                throw new ThreadStateException();
            AbortAllThreads();
            mainThread.Join(); //Not strictly necessary but good for debugging as ensures the thread is actually closed;
        }

        void ThreadLoop(Action LoopedFunction, int loopFrequency)
        {
            
            try
            {
                long durationTicks = 1 / loopFrequency * Stopwatch.Frequency; //seconds * ticks/second 
                while (true)
                {
                    //Note: Could be multithreaded further but sticking with one manager thread for now
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();

                    LoopedFunction();

                    //TODO: add exception handling and recovery for thread 

                    //loop repeats at a maximum of a given frequency (60 frames per second?)
                    if (durationTicks < stopwatch.ElapsedTicks)
                    {
                        //Sleep till next loop rather than block to free up resources
                        Thread.Sleep((int)Math.Ceiling((double)((durationTicks - stopwatch.ElapsedTicks) / (Stopwatch.Frequency * 1E3))));
                    }
                    stopwatch.Reset();
                }
            }
            catch (ThreadAbortException)
            {
                //TODO: Release resources
                Clean();
                return;
            }
            catch //if the thread crashes for unknown reason stop other threads
            {
                //TODO: Release resources
                AbortAllThreads();
                Clean();
                throw; //throws the error on
            }
        }

        void AbortAllThreads()
        {
            if(mainThread.IsAlive)
                mainThread.Abort();
            if (decodeThread.IsAlive)
                decodeThread.Abort();
        }

        //Manager performs a series of checks and performs certain actions each loop depending on the state of the various buffers
        void ManagerLoop()
        {
            PackageAndSendMessages();
        }

        void PackageAndSendMessages()
        {
            //If the ack buffer is full do not send or add to the packet queue
            //Fill the packet queue or empty message queue
            while (!messageQueue.IsEmpty && !packetQueue.IsFull)
            {
                lock (messageQueueLock)
                {
                    //create next packet from message queue
                    CreateNextPacketFromQueue();
                }
                //For efficiency asynchronusly send packets while adding to the packet queue
                if (!packetQueue.IsEmpty)
                    SendNextPacketAsync();
            }
            //If there are any more packets waiting to be sent after this process (packets added by ack logic)
            //Send all packets waiting to be sent
            while (!packetQueue.IsEmpty)
            {
                SendNextPacketAsync();
            }
        }

        public void QueueMessage(Message m)
        {
            lock (messageQueueLock) //Ensure no conflict between manager thread and public add message method
            {
                for (int i = messageQueue.Length - 1; i >= 0; i--)
                {
                    if (messageQueue[i].Priority >= m.Priority)
                    {
                        try
                        {
                            messageQueue.InsertAt(i, m);
                        }
                        catch (QueueFullException)
                        {
                            throw;
                        }
                    }
                }
            }
        }

        //Takes the next packet to be sent, attempts to send and moves to awaiting ack buffer if reliable
        IAsyncResult SendNextPacketAsync()
        {
            //Get the packet to be sent
            Packet p;
            lock (packetQueueLock)
            {
                if (!packetQueue.IsEmpty)
                {
                    p = packetQueue.PopFront();
                }
                else //Only called in this case when ack needs to be sent
                {
                    p = new Packet(currentPacketID, lastSentAckId, lastSentAckInfo, PacketType.Ack, salt);
                }
            }

            //Add latest ack info to the packet
            lock (ackInfoLock)
            {
                if (!ackQueue.IsEmpty)
                {
                    (lastSentAckId, lastSentAckInfo) = ackQueue.PopFront();
                }
                p.AckId = lastSentAckId;
                p.AckedBits = lastSentAckInfo;
            }

            //Serialize the packet and send
            byte[] data = p.Serialize();
            IAsyncResult task = socket.SendAsync(data);

            //If reliable, add to the awating ack buffer
            if (!(p.Type == PacketType.dataUnreliable || p.Type == PacketType.Ack)) //Unreliable and ack packets are fire and forget
            {
                p.StripUnreliableMessages();
                awaitingAckBuffer.Add(p.Id, p);
            }

            return task;
        }

        void QueuePacket(Packet p)
        {
            lock (packetQueueLock)
            {
                for (int i = packetQueue.Length - 1; i >= 0; i--)
                {
                    if (packetQueue[i].Priority >= p.Priority)
                    {
                        try
                        {
                            packetQueue.InsertAt(i, p);
                        }
                        catch (QueueFullException)
                        {
                            throw;
                        }
                    }
                }
            }
        }

        //Checks message queue and adds as many messages to the new packet as can fit seperated by a delimiter
        //The new packet is sent to the packetQueue
        //Note: only for use after connection is established
        void CreateNextPacketFromQueue()
        {
            if (messageQueue.Length > 0)
            {
                //Check front of queue for large payload packet (should only be sent on resync or whilst loading so should have high priority and not get pushed back easily)
                if (messageQueue[0].Length > _maxPacketDataBytes)
                {

                    CreateFragmentedPackets();
                }
                else
                {
                    CreateMultiMessagePacket();
                }
            }
            else
            {
                throw new QueueEmptyException();
            }
        }

        //Creates and adds multiple packets to the queue from one message (use sparingly as packet fragmentation is expensive)
        void CreateFragmentedPackets()
        {
            throw new NotImplementedException();
        }

        void CreateMultiMessagePacket()
        {
            //Only one packet to create
            PacketType packetType = PacketType.dataUnreliable;
            byte priority = messageQueue[0].Priority;
            int packedSize = 0;

            //Calculate how many messages fit in packet
            int messageCount = 0;
            int length = messageQueue.Length; //as messageQueue.length changes within the loop we need to cashe the initial length 
            for (int i = 0; i < length; i++)
            {
                if (messageQueue[i].Length + packedSize > _maxPacketDataBytes)
                {
                    messageCount = i;
                    break;
                }
                else
                {
                    packedSize += messageQueue[i].Length;
                }
            }

            //Declare and populate packed message array
            Message[] messages = new Message[messageCount];
            for (int i = 0; i < messages.Length; i++)
            {
                messages[i] = messageQueue.PopFront();
                if (messages[i].IsReliable)
                {
                    packetType = PacketType.dataReliable;
                }
            }
            //Create packet from stream
            Packet p = new Packet(currentPacketID, lastSentAckId, lastSentAckInfo, packetType, salt, messages, priority);
            QueuePacket(p);
            if (p.Type != PacketType.dataUnreliable) //unreliable packets are shoved on a seperate queue when received rather than normal received buffer
                currentPacketID += 1;
        }

        void RequeueUnacknowledgedPackets()
        {
            //Find ids to resend (anything unacknowledged older than a few packets to avoid resending late arrivals)) 
            lock (ackInfoLock)
            {

            }
            //Move stripped packet from awaiting ack to front of send queue for manager thread to deal with
            //No need to clear entry, packets will overwrite when sent again

            //TODO Edgecase: packet resend is requested, packet arrives before resend processed, acknowledged and removed from resend buffer, but readded when sent... 
            //solved by locking queue, ack mask and ack buffer before sending, within lock check if acknowledged before sending and releasing 
            throw new NotImplementedException();
        }

        //

        //Sending unreliable data
        //Send off with x packets and forget about it

        //Sending reliable data
        //Send off with every x packets till ack or limit (short time to avoid unfair rubberbanding from other client perspective but still playable to poor connection client)

        //Sending reliable temporal data (like player inputs)
        //Send off with every x packets till ack or limit, order inputs at decode, promote priority if delayed

        //Sending reliable Trigger data (like scene change or door opening)
        //Much longer time limit, if exceded likely a complete disconnect, client will need to be sent a complete resync when connection regained;

        //Each message will need an id so the receiver can identify duplicates
        //All reliable messages only differ in timeouts, intervals, burst sizes and priority to packet manager. actual method remains the same.
        //likely a fair few trash messages taking up bandwidth 
        //If acks are recieved this is efficient

        //Unreliable packets dont need an id as they have no order?
        //Could either send with no id (prefix header with type first) or add a whole system for figuring out which missing ids have reliable messages;   


        //Ack & resend system:
        //  received packets placed in input buffer.
        //  decode in order received
        //  if unreliable data packet just decode and apply asap (still send ack back) (can be sent with id too but just ignore, maybe seperate buffer?)
        //  Include latest ack info in unreliable packets as to not dry ack connection, also reply with latest ack when unreliable received and send buffer empty
        //  if reliable-ordered and none missing, decode and apply and send ack back;
        //  if reliable-ordered and missing id's, wait till out of order packets received and reorder.
        //  if missing id's not received for ack encoding length (32 atm), resend (buffer any extra packets received in mean time).
        //  if large gap from latest id at sender, block sending buffer and request resend from last highest received (likely a burst of packet loss).

        //---------------------RECEIVE METHODS-----------------------
        ushort latestResponseAck = ushort.MaxValue;
        volatile ushort latestPacketIdReceived = ushort.MaxValue; //value set to id of latest received packet
        ushort lastPacketIdDecoded = ushort.MaxValue; //indicates the id of the last packet taken off the receive buffer 


        //Method called when data received from socket (I believe this uses the socket's thread)
        void OnReceive(byte[] data, int bytesRead)
        {
            Packet.Header header;
            try
            {
                header = Packet.DeserializeHeaderOnly(data);
            }
            catch (PacketChecksumException)
            {
                return;//Ignore any packets with invalid checksum
            }

            //Basic checks to discard any simple spoofed packets
            if (header.packetType == PacketType.ClientConnectionRequest)
            {
                //If client is already connected, ignore packet and do not buffer
                if (isConnected) return;

                //TODO: If a client receives this message type, ignore
            }
            else if (header.packetType == PacketType.ServerChallengeRequest)
            {
                //if the client has already established connection, ignore
                if (isConnected) return;

                //TODO: If the server recieves this ingnore
            }
            //Check salt if not a connection request
            else if (header.salt != salt)
            {
                //Ignore packet if salt does not match (Note: could be made a rolling salt by sending random seed rather than xor?, getting into encryption territory here.)
                return;
            }

            //Once passed basic tests, Store in buffer for manager to handle when ready 
            if (header.packetType == PacketType.dataUnreliable || header.packetType == PacketType.Ack)
            {
                if (IsIdNewerThan(header.id, lastPacketIdDecoded)) //Ensures the received unreliable packet is not very old or very new
                {
                    //Unreliable packets do not need to be ordered so store in queue
                    if (!packetQueue.IsFull)
                    {
                        lock (receiveQueueLock)
                        {
                            receiveQueue.Add((header, data));
                        }
                    }
                }
            }
            else
            {
                if (IsIdNewerThan(header.id, lastPacketIdDecoded))
                {
                    
                    //Reliable messages have an order achieved by indexing recieveBuffer by packet id    
                    lock (receiveBufferLock)
                    {
                        receiveBuffer.Add(header.id, (header, data));
                        if (IsIdNewerThan(header.id, latestPacketIdReceived)) latestPacketIdReceived = header.id;
                    }
                }
                //discard otherwise (most likely duplicate)
            }

        }

        void ReceiveLoop()
        {
            ReceiveAllAndSendAcks();
        }

        //Decodes all buffered reliable and unreliable messages, requests resend of missing reliable packets 
        void ReceiveAllAndSendAcks()
        {
            //Note: current method could lead to longer than necessary lag times when a packet is missed/
            //as it waits to deserialize rather than deserializing what it has and buffering

            Message[][] allOrderedDecodedMessages = new Message[_receiveBufferSize + _receiveQueueSize][];
            int orderedMessagesIndexer = 0;
            
            //------RELIABLE PACKET HANDLING----------
            //Decode valid in receiveBuffer first (gives reliable packets slightly higher priority)
            //Check if a new reliable message has been received
            if (lastPacketIdDecoded != latestPacketIdReceived)
            {
                bool keepDecoding = true;

                //Loop through headers from last decoded packet to latest received packet
                for (ushort id = (ushort)(lastPacketIdDecoded + 1); id != (ushort)(latestPacketIdReceived + 1); id++)
                {
                    try
                    {
                        if (receiveBuffer.IsIdBuffered(id))
                        {
                            Packet.Header h;
                            byte[] data;
                            lock (receiveBufferLock)
                            {
                                (h, data) = receiveBuffer.Get(id); //Read the packet info

                                //Apply ack info to ack masks 

                                //Check received encoded ack is within valid range
                                if (IsIdNewerThan(h.ackId, latestResponseAck))
                                {
                                    latestResponseAck = h.ackId;
                                    sentPacketAcks.AddEncodedAck(h.ackedBits, h.ackId);
                                }
                                else if (IsIdOlderThan(h.ackId, latestResponseAck))
                                {
                                    sentPacketAcks.AddEncodedAck(h.ackedBits, h.ackId);
                                }
                                else
                                {
                                    //Something is wrong with the ack as it falls outside the acceptable range, should never send these intentionally
                                    //throw an error to ignore and treat as not received
                                    receiveBuffer.Remove(id);
                                    keepDecoding = false;
                                    throw new PacketEncodedAckException();
                                }
                            }

                            //Deserialize the packet messages if the packet is inorder
                            if (keepDecoding)
                            {
                                (_, allOrderedDecodedMessages[orderedMessagesIndexer]) = Packet.Deserialize(data);
                                orderedMessagesIndexer++;
                                lastPacketIdDecoded = id;
                            }

                            //Id of packet is already within acceptable range and the packet cannot be an ack if reliable
                            receivedPacketAcks[id] = true;

                            //TODO: not a huge fan of this system, for sending acks, seperate sender thread could help 
                            //Add an ack for the packet to the send queue and fire
                            AckBitArray encodedAck = receivedPacketAcks.GetEncodedAck(id, Packet.ackedBytesLength);
                            lock (ackInfoLock)
                            {
                                ackQueue.Add((id, encodedAck));
                            }
                            SendNextPacketAsync(); //Note: by sending here we guarantee the ack is sent

                        }
                        else
                        {
                            keepDecoding = false;
                        }
                    }
                    catch (PacketEncodedAckException) { } //Dont crash out as could happen normally due to packet manipulation
                }

            }

            //------UNRELIABLE PACKET HANDLING----------
            //Decode all unreliable messages in receiveQueue
            lock (receiveQueueLock)
            {
                while (!receiveQueue.IsEmpty)
                {
                    (Packet.Header h, byte[] data) = receiveQueue.PopFront();

                    //apply encoded acks to ack mask (build picture of which reliable messages sent have been received)
                    receivedPacketAcks.AddEncodedAck(h.ackedBits, h.ackId);

                    (_, allOrderedDecodedMessages[orderedMessagesIndexer]) = Packet.Deserialize(data);
                    orderedMessagesIndexer++;
                }
            }
        }

        //Checks if the provided id arrived within the acceptable range older than the provided max id, maxId inclusive
        bool IsIdOlderThan(ushort id, ushort maxId)
        {
            return IsIdNewerThan(id, (ushort)(maxId - _maxMissedPackets));
        }

        //Checks if the provided id arrived within the acceptable range newer than the provided oldId, oldId exclusive
        bool IsIdNewerThan(ushort id, ushort oldId)
        {
            //Acceptable range is from old id exclusive to last decoded wrapped once inclusive
            //receiveBuffer.length << ushort.MaxValue
            ushort LB = (ushort)(oldId + 1);
            ushort UB = (ushort)(oldId + _maxMissedPackets);

            if (LB < UB) //No overflow
            {
                if (LB <= id && id <= UB) return true;
                else return false;
            }
            else //(UB < LB) UB overflows
            {
                if (LB <= id || id <= UB) return true;
                else return false;
            }
        }

        //OBSELETE
        /*
        IdBuffer.InputIdState CheckReceivedPacketId(ushort idReceived)
        {
            //if more than the encoded bits are missed packet should be treated as missed and a resend from latest received should be requested.
            const UInt16 maxMissedPackets = Packet.ackedBitsLength;

            //Get index of first 0 in acknowledgment bit array
            int i;
            for (i = Packet.ackedBitsLength - 1; i >= 0; i--)
            {
                //Check for first unacked message
                if (!ackedBits[i])
                {
                    break;
                }
            }
            //Calculate id of oldest unacked message 
            ushort oldestUnAckedId = (ushort)(latestPacketIdReceived - (Packet.ackedBitsLength - i + 1));
            ushort newAndAcceptableLimit = (ushort)(oldestUnAckedId + maxMissedPackets);
            ushort oldAndAcceptableLimit = (ushort)(latestPacketIdReceived - maxMissedPackets);

            //3 ouput cases: invalid, old message, new message
            IdBuffer.InputIdState state = IdBuffer.GetIdBufferEntryState(idReceived, latestPacketIdReceived, oldAndAcceptableLimit, newAndAcceptableLimit);
            return state;
        }
        */



        //OBSELETE Assumes id has been checked and updates ack info accordingly 
        /*
        void UpdateAckInfo(IdBuffer.InputIdState state, ushort idReceived)
        {
            if (state == IdBuffer.InputIdState.Invalid)
                return; //no update for invalid packet

            //Calculate acked bit array
            AckBitArray ackBitArray = new AckBitArray(ackedBits); //Create new copy whilst bit manipulating
            bool[] overflows = new bool[Packet.ackedBitsLength];
            int idDiff;
            switch (state)
            {
                case IdBuffer.InputIdState.New:
                    idDiff = (idReceived - latestPacketIdReceived) % Packet.ackedBitsLength;//Should handle overflow cases
                    //latest bit is at index 0 so left shift acks by diff and throw error if unacked packet found;
                    overflows = ackBitArray << idDiff;
                    //Check for unacknowledged packets
                    foreach (var overflow in overflows)
                    {
                        if (!overflow)
                            throw new PacketNotAcknowledgedException();
                    }
                    ackBitArray[0] = true;
                    latestPacketIdReceived = idReceived;
                    break;
                case IdBuffer.InputIdState.Old:
                    //Set acknowledgment bit for id position to true;
                    idDiff = (latestPacketIdReceived - idReceived) % Packet.ackedBitsLength;
                    ackBitArray[idDiff] = true;
                    break;
                default:
                    return;
            }
            ackedBits = ackBitArray;
        }
        */

        static ulong GetNewSalt()
        {
            return BitConverter.ToUInt64(GetNewSalt(sizeof(ulong)), 0);
        }

        static byte[] GetNewSalt(int maximumSaltLength)
        {
            var salt = new byte[maximumSaltLength];
            random.GetNonZeroBytes(salt);
            return salt;
        }
    }
}
