using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Timers;
namespace UnityNetworkingLibrary
{
    
    
    //Mangages ordering and priority of packets to send
    //Options: unreliable, reliable and blocking data in packets
    //  unreliable packets are completely unreliable data, sent in one burst and forgotten
    //  reliable packets contain at least one reliable message, receive ack before forgotten. resent if no ack recieved.
    //      -various tiers for this which vary burst size and timeout. (larger bursts and timeouts for time critical flags)
    //  blocking packets behave like TCP, only communicating that packet till received ack (for things like end of game)
    class PacketManager //TODO: decide split into client and server variants/one packet manager per connection;
    {
        internal const int _maxPacketSizeBytes = 1024;
        internal const int _maxPacketSizeBits = 8 * _maxPacketSizeBytes;
        internal const int _messageQueueSize = 200;
        internal const int _packetQueueSize = Packet.ackedBitsLength; //At Least long enough to accomodate all encoded acks
        internal const int _awaitingAckBufferSize = Packet.ackedBitsLength; //At Least long enough to accomodate all encoded acks
        internal const int _receiveBufferSize = Packet.ackedBitsLength; //At Least long enough to accomodate all encoded acks
        //Define constants 
        //TODO: burst length and interval/timeout could be made dynamic based on connection or completely user defined
        const int _unreliableBurstLength = 3; //Number of packets data is included in when sent
        const int _reliableBurstLength = 3; // " for reliable data
        const int _reliableTriggerBurstLength = 10; // " for reliable trigger data
        TimeSpan _reliableInterval = TimeSpan.FromMilliseconds(100); //timeout after reliable burst before assuming not received in ms NOTE: should be rarely triggered thanks to ackedbits
        BitArray ackedBits = new BitArray(Packet.ackedBitsLength); //Acknowledgement bits for the last ackedBitsLength received packet ids
        UInt16 lastPacketIdReceived = 0; //value set to id of latest receieved packet
        UInt16 currentPacketID = 0; //value incremented and assigned to each packet in the send queue
        IndexableQueue<Message> messageQueue; //Messages to be sent are added to this queue acording to priority.
        IndexableQueue<Packet> packetQueue; //prepared packets waiting to be sent, fairly short queue which allows pushing of unacknowledged packets to front 
        PacketBuffer awaitingAckBuffer; //buffer storing reliable messages awaiting acknowledgement.
        PacketBuffer receiveBuffer; //When reliable packet id skipped, any more received packets are added to this buffer while waiting for resend.

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

        public struct Message
        {
            public enum MessageType
            {
                unreliable,
                reliable,
                reliableTemporal,
                reliableTrigger,
            }

            public MessageType Type { get; private set; }
            public int Priority { get; set; }
            public byte[] Data { get; private set; }
            
            public Message(int priority, MessageType type, byte[] data)
            {
                this.Type = type;
                this.Priority = priority;
                this.Data = data;
            }
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
            catch (ExceptionExtensions.QueueEmptyException)
            {
                //If the queue is empty return null
                return null;
            }
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



        static RNGCryptoServiceProvider random = new RNGCryptoServiceProvider(); //Secure random function

        
        static byte[] GetSalt()
        {
            return GetSalt(Packet.saltLengthBits);
        }
        static byte[] GetSalt(int maximumSaltLength)
        {
            var salt = new byte[maximumSaltLength];
            random.GetNonZeroBytes(salt);
            return salt;
        }

        //At a basic level Every packet needs a checksum (CRC32), dataformat, salt (server or client or xor), data 
        
    }
}
