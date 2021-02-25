using System;
namespace UnityNetworkingLibrary
{
    /* Contains Highest level methods for establishing connections
     */
    public static class NetworkManager
    {
        //Attempts to send connection request to target server using the given socket
        //Returns 0 if successfully connected, returns negativve error code otherwise
        //This is a blocking function and should only be called in its own thread as to not block the game from updating.
        internal static void ClientConnect(UDPSocket socket, string targetIP, string targetPort, int packetBurst = 3)
        {

            //Send connection request packet x times 
            //Begins checking received data for challenge request 
            //If Timeout send try X more times
            //If still timeout throw ConnectionFailedException

            //If challenge request recieved 
            //Send challenge response reliably?
            //calculate and assign xor salt for subsequent packets
            //Start polling server to maintain connection
        }
    }
}
