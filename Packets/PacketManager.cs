using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
namespace UnityNetworkingLibrary
{
    using ExceptionExtensions;
    using Utils;
    using Messages;
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
        public const int _packetQueueSize = Packet.ackedBitsLength; //At Least long enough to accomodate all encoded acks
        public const int _awaitingAckBufferSize = Packet.ackedBitsLength; //At Least long enough to accomodate all encoded acks
        public const int _receiveBufferSize = Packet.ackedBitsLength; //At Least long enough to accomodate all encoded acks


        //Define constants 
        //TODO: burst length and interval/timeout could be made dynamic based on connection or completely user defined
        const int _unreliableBurstLength = 3; //Number of packets data is included in when sent
        const int _reliableBurstLength = 3; // " for reliable data
        const int _reliableTriggerBurstLength = 10; // " for reliable trigger data
        TimeSpan _reliableInterval = TimeSpan.FromMilliseconds(100); //timeout after reliable burst before assuming not received in ms NOTE: should be rarely triggered thanks to ackedbits
        AckBitArray ackedBits = new AckBitArray(Packet.ackedBitsLength); //Acknowledgement bits for the last ackedBitsLength received packet ids
        UInt16 latestPacketIdReceived = 0; //value set to id of latest receieved packet
        UInt16 currentPacketID = 0; //value incremented and assigned to each packet in the send queue
        IndexableQueue<Message> messageQueue; //Messages to be sent are added to this queue acording to priority.
        IndexableQueue<Packet> packetQueue; //prepared packets waiting to be sent, fairly short queue which allows pushing of unacknowledged packets to front 
        PacketBuffer awaitingAckBuffer; //buffer storing reliable messages awaiting acknowledgement.
        PacketBuffer receiveBuffer; //When reliable packet id skipped, any more received packets are added to this buffer while waiting for resend.

        static RNGCryptoServiceProvider random = new RNGCryptoServiceProvider(); //Secure random function
        ulong salt;

        UDPSocket socket;

        PacketManager(UDPSocket sock)
        {
            //define arrays
            messageQueue = new IndexableQueue<Message>(_messageQueueSize);
            packetQueue = new IndexableQueue<Packet>(_packetQueueSize);
            awaitingAckBuffer = new PacketBuffer(_awaitingAckBufferSize);
            receiveBuffer = new PacketBuffer(_receiveBufferSize);
            socket = sock;
        }

        //TODO: Packet Manager should be its own thread
        //Initiliaize Manager


        //Event handle 
        public void OnReceive()
        {

        }

        

        public void QueueMessage(Message m)
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

        //Returns the next packet to be sent and moves to awaiting ack buffer if reliable
        Packet PopNextPacket()
        {
            try
            {
                Packet packet = packetQueue.PopFront();
                if (PacketType.dataReliable == packet.Type)
                {
                    awaitingAckBuffer.AddPacket(packet);
                }
                return packet;
            }
            catch (QueueEmptyException)
            {
                //If the queue is empty return null
                return null;
            }
        }

        void QueuePacket(Packet p)
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
            Packet p = new Packet(currentPacketID, latestPacketIdReceived, ackedBits, packetType, salt, messages, priority);
            QueuePacket(p);
            if(p.Type != PacketType.dataUnreliable) //unreliable packets are shoved on a seperate queue when received rather than normal received buffer
                currentPacketID += 1;

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
        //  if reliable-ordered and none missing decode and apply and send ack back;
        //  if reliable-ordered and missing id's wait till out of order packets received and reorder.
        //  if at sender ack recieved indicates missing reliable packets resend else 
        //  if missing id's not received for ack encoding length (32 atm), (buffer any extra packets received in mean time).
        //  if large gap from latest id, buffer and request resend from last highest received (likely a burst of packet loss).



        //Returns an enumeration indicating wether the new packet is old, new or invalid 
        PacketBuffer.IdBufferEntryState CheckReceivedPacketId(ushort idReceived)
        {
            //if more than the encoded bits are missed packet should be treated as missed and a resend from latest received should be requested.
            const UInt16 maxMissedPackets = Packet.ackedBitsLength;

            //Get index of first 0 in bit array
            int i = 0;
            for (i = 0; i < Packet.ackedBitsLength; i++)
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
            PacketBuffer.IdBufferEntryState state = PacketBuffer.GetIdBufferEntryState(idReceived, latestPacketIdReceived, oldAndAcceptableLimit, newAndAcceptableLimit);
            return state;
        }

        //Assumes id has been checked and updates ack info accordingly 
        void UpdateAckInfo(PacketBuffer.IdBufferEntryState state, ushort idReceived)
        {
            if (state == PacketBuffer.IdBufferEntryState.Invalid)
                return; //no update for invalid packet

            //Calculate acked bit array
            AckBitArray ackBitArray = new AckBitArray(ackedBits); //Create new copy whilst bit manipulating
            bool[] overflows = new bool[Packet.ackedBitsLength];
            int idDiff;
            switch (state)
            {
                case PacketBuffer.IdBufferEntryState.New:
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
                case PacketBuffer.IdBufferEntryState.Old:
                    //Set acknowledgment bit for id position to true;
                    idDiff = (latestPacketIdReceived - idReceived) % Packet.ackedBitsLength;
                    ackBitArray[idDiff] = true;
                    break;
                default:
                    return;
            }
            ackedBits = ackBitArray;
        }

        static byte[] GetNewSalt()
        {
            return GetNewSalt(Packet.saltLengthBits);
        }
        static byte[] GetNewSalt(int maximumSaltLength)
        {
            var salt = new byte[maximumSaltLength];
            random.GetNonZeroBytes(salt);
            return salt;
        }
    }
}
