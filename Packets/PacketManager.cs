using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
namespace UnityNetworkingLibrary
{
    using ExceptionExtensions;
    using Utils;
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

        //Returns the next packet to be sent and moves to awaiting ack buffer if reliable
        public Packet PopNextPacket()
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

            //TODO: If it's in the message queue order should not matter, so pack as many as possible
            MemoryStream stream = new MemoryStream(_maxPacketSizeBytes);
            CustomBinaryWriter writer = new CustomBinaryWriter(stream);
            int length = messageQueue.Length; //as messageQueue.length changes within the loop we need to cashe the initial length 
            for (int i = 0; i < length; i++)
            {
                if (messageQueue[0].Length + packedSize <= _maxPacketSizeBytes)
                {
                    Message m = messageQueue.PopFront();
                    if (m.IsReliable)
                    {
                        packetType = PacketType.dataReliable;
                    }
                    packedSize += m.Length;
                    m.Serialize(writer);
                }
                else
                {
                    //Stop if cant fit the next message
                    break;
                }
            }
            //Create packet from stream
            Packet p = new Packet(currentPacketID, latestPacketIdReceived, ackedBits, packetType, salt, stream.ToArray(), priority);
            QueuePacket(p);
            currentPacketID += 1;

            stream.Dispose();
            writer.Dispose();
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


        //TODO: Some sort of queue and priority system for sending and receiving packets. 
        //One main packaging queue of messages, ordered by priority
        //messages are taken off the queue and assigned a packet id, a burst of that packet is sent, any unreliable data is stripped (or timed out data), and if the packet has any more data it awaits ack or retransmission.   
        //2 options here could either:
        //      1. have rolling bursts where each message has its own id attached and duplicates are detected on decode.
        //          -leads to larger packets and more processing overhead but data can be packaged and sent more efficiently (so less total packets)
        //          -Adds a LOT of complexity in how packets are created
        //      2. just send each packet x times (dependant on packet type) so only checks packet id on decode
        //          -leads to smaller packet size but likely more packets sent
        //          -any unreliable data can be easily filtered out (flag stored locally) before the packet is resent.
        //          -easily check for duplicate data from packet id 
        //Prefer option 2, combined with a Selectve repeat Protocol

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
