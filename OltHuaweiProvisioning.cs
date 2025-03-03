//Rextester.Program.Main is the entry point for your code. Don't change it.
//Compiler version 4.0.30319.17929 for Microsoft (R) .NET Framework 4.5

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace OltHuaweiProvisioning
{
    public class Program
    {
        public static void Main(string[] args)
        {
            StringBuilder configurationScript;
            
            // Parameters
            string clientLogin = "IANNAWALLACE"; string vlanID = "190"; string fsp = "0/1/2"; string sn = "48575443B238C79A";
           
            try
            {
                ONU onu = new ONU(clientLogin, vlanID, fsp, sn);
                //Console.WriteLine(onu.ClientLogin);
                //Console.WriteLine(onu.VLANID);
                //Console.WriteLine(onu.FSP);
                //Console.WriteLine(onu.SN);
                //Console.WriteLine(onu.ConfigurationScript);
                
                TelnetConnection telnetClient = new TelnetConnection(onu, "172.16.2.19", 23);

                // Connect the client
                telnetClient.Disconnect();
            }
            catch (FormatException ex)
            {
                Console.WriteLine("Alguns parâmetros numéricos não estão corretos, favor verificar os campos VLAN ID e FSP.");
            }
            catch (SNException ex)
            {
                Console.WriteLine("Há algum erro no SN informado, favor verificar e tentar novamente.");
            }
            catch (ClientLoginException ex)
            {
                Console.WriteLine("Há algum erro no login do cliente, favor verificar e tentar novamente.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            
        }
        
        // Creates the SN Exception
        class SNException : Exception
        {
            
        }
         
        // Creates the Client Login Exception
        class ClientLoginException : Exception
        {
            
        }
        
        class ONU 
        {
            public string ClientLogin {get; private set;}
            public string FSP {get; private set;}
            public string VLANID {get; private set;}
            public string SN {get; private set;}
            public StringBuilder ConfigurationScript {get; private set; }
            
            public ONU(string clientLogin, string vlanID, string fsp, string onuSN)
            {
                // Verify the parameters before intializing properties
                this.VerifyONUParams(clientLogin, vlanID, fsp, onuSN);
                
                // Build the configuration script that
                // will be sended to telnet server
                BuildConfigurationScript();
            }
            
            private void BuildConfigurationScript()
                string[] FSPCheck = this.FSP.Split('/');
                
                // Building ONU configuration script - accessing the interface
                string command = FSPCheck[0] + "/" + FSPCheck[1];
                ConfigurationScript = new StringBuilder("interface gpon " + command + "\n");
                
                // Building ONU configuration script - adding ONU to the interface
                command = "ont add " + FSPCheck[2] + " sn-auth " + SN + " omci ont-lineprofile-id " + VLANID + " ont-srvprofile-id " + VLANID + " desc " + ClientLogin;
                ConfigurationScript.AppendLine(command);
                
                // Building ONU configuration script - enabling ont port native vlan
                // The command will be completed at execution time, replacing # by ONT ID (server answers)
                command = "ont port native-vlan " + FSPCheck[2] + " # eth 1 vlan " + VLANID;
                ConfigurationScript.AppendLine(command);
                ConfigurationScript.AppendLine(""); // necessary to aswer the server request
                
                // Building ONU configuration script - getting out from the interface
                ConfigurationScript.AppendLine("quit");
                
                // Building ONU configuration script - enabling service-port
                command = "service-port vlan " + VLANID + " gpon " + FSP + " ont # gemport 1 multi-service user-vlan " + VLANID;
                ConfigurationScript.AppendLine(command);
                ConfigurationScript.AppendLine(""); // necessary to aswer the server request
                
                // Building ONU configuration script - accessing interface
                command = "interface gpon " + FSPCheck[0] + "/" + FSPCheck[1];
                ConfigurationScript.AppendLine(command);
                
                // Building ONU configuration script - displaying ONU optical-info
                command = "display ont optical-info " + FSPCheck[2] + " # ";
                ConfigurationScript.AppendLine(command);
                
                // Building ONU configuration script - getting out from the interface
                ConfigurationScript.AppendLine("quit");
            }
            
            private void VerifyONUParams (string clientLogin, string vlanID, string fsp, string sn)
            {
                
                // Verify VLANID input
                int vlanIDCheck = Convert.ToInt32(vlanID);
                
                // Check client login if is not null
                if (clientLogin.Length <= 0)
                     throw new ClientLoginException();
                
                // Check if SN's lenght is 16
                if(sn.Length != 16)
                     throw new SNException();

                // Verify FSP input
                string[] fspCheck = fsp.Split('/');
                for(int i = 0; i < fspCheck.Length; i++)
                {
                    
                    // Check if Frame, Slot and Port are numbers
                    int fspCheckingValue = Convert.ToInt32(fspCheck[i]);
                    
                    // Check if they are greater then 16 or less then 0
                    // ---> permitted values are between 0 and 15, including 0 and 15
                    if (fspCheckingValue < 0 || fspCheckingValue > 15)
            {
             
                // Getting Frame, Slot and Port values
                        throw new FormatException();
                    
                    // Builting property FSP
                    FSP = FSP + fspCheckingValue.ToString() + "/";
                    
                }
                
                // Removes the last "/" from FSP
                FSP = FSP.Remove(FSP.Length - 1);
                
                // Here everyting is ok, so intialize properties
                ClientLogin = clientLogin;
                VLANID = vlanID;
                this.SN = sn;
            }
        }
        
        class TelnetConnection
        {
            private NetworkStream telnetStream_A;
            private TcpClient telnet_A;
            private Thread t; 
            private bool telnetReceiving = false;
            private string telnetOut;
            private delegate void Display(string s);
            private IPAddress serverIP;
            private int serverPort;
            private ONU onu;

            public TelnetConnection(ONU onu, string serverIP, int serverPort)
            {
                this.onu = onu;
                this.serverPort = serverPort;
                this.serverIP = IPAddress.Parse(serverIP);
            }
            
            public void Connect()
            {
                try
                {
                    // Creates Tcp Client for connection
                    telnet_A = new TcpClient();
                    
                    // Sets maximum receiving and sending time
                    telnet_A.SendTimeout = 1000;
                    telnet_A.ReceiveTimeout = 1000;
                    telnet_A.Connect(this.serverIP, this.serverPort);
                    
                    if(telnet_A.Connected)
                    {
                        // Takes the data stream from tcp client
                        telnetStream_A = telnet_A.GetStream();
                        
                        telnetReceiving = true;
                        t = new Thread(ReceiveData);
                        t.Start();
                        Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            
            public void Disconnect()
            {
               try
               {
                   telnet_A.Close();
               }
               catch (NullReferenceException ex)
               {
                   Console.WriteLine("O cliente telnet não está conectado ao servidor, favor conecte-se e tente novamente.");
               }
               catch (Exception ex)
               {
                   Console.WriteLine(ex.GetType());
               }
            }
            
            private void ReceiveData()
            {
                try
                {
                    while(telnetReceiving)
                    {
                        if(telnetStream_A.DataAvailable)
                        {
                            byte[] readingBuffer = new byte[telnet_A.ReceiveBufferSize];
                            
                            // buffer - an array of type Byte that is the location in memory to store data read
                            // from the NetworkStream [in this case, readingBuffer]
                            //
                            // offset - the location in buffer to begin storing the data to [in this case, 0]
                            //
                            // size - The number of bytes to read from the NetworkStream [in this case, the .ReceiveBufferSize]
                            int numBytesRead = telnetStream_A.Read(readingBuffer, 0, (int)telnet_A.ReceiveBufferSize);
                            Array.ReferenceEquals(ref readingBuffer, numBytesRead);
                            
                            this.telnetOut = Encoding.ASCII.GetString(readingBuffer);
                            
                            PrintDataRead();
                            
                            Thread.Sleep(20);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
                
            public string PrintDataRead()
            {
                return this.telnetOut;
            }
            
            private void SendData(string s)
            {
                byte[] sendBuffer;
                
                if(s != null)
                {
                    try
                    {
                        sendBuffer = Encoding.ASCII.GetBytes(s + "\r");
                        telnetStream_A.Write(sendBuffer, 0, sendBuffer.Length);
                        
                        Thread.Sleep(20);
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
        }
    }
}
