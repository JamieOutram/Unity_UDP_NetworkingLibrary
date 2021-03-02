using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Timers;
namespace UnityNetworkingLibrary
{
    
    
    //Mangages ordering and priority of packets to send
    class PacketManager
    {
        internal const int _maxPacketSizeBytes = 1024;
        internal const int _maxPacketSizeBits = 8 * _maxPacketSizeBytes;
        

        const int _unreliableBurstLength = 3; //Number of packets data is included in when sent
        const int _reliableBurstLength = 3; // " for reliable data
        const int _reliableTriggerBurstLength = 10; // " for reliable trigger data
        TimeSpan _reliableInterval = TimeSpan.FromMilliseconds(100); //timeout after reliable burst before assuming not received in ms NOTE: hopefully rarely triggered thanks to ackedbits
        BitArray ackedBits = new BitArray(Packet.ackedBitsLength); //Acknowledgement bits for the last ackedBitsLength received packet ids
        UInt16 lastPacketIdReceived = 0; //value set to id of latest receieved packet
        UInt16 currentPacketID = 0; //value incremented and assigned to each packet in the send queue
        Message[] messageQueue; //Messages to be sent are added to this queue acording to priority.
        Packet[] packetQueue; //prepared packets waiting to be sent, fairly short queue which allows pushing of unacknowledged packets to front 
        Packet[] awaitingAckBuffer; //buffer storing reliable messages awaiting acknowledgement.
        Packet[] receiveBuffer; //When reliable packet id skipped, any more received packets are added to this buffer while waiting for resend.


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

        //Sends data packet and awaits sending confirmation if not received, sends again, repeats until standard timeout.
        private static void SendReliable(PacketType type, byte[] data)
        {
        }



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
