using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Globalization;

public class UdpClientPong : MonoBehaviour
    {
        UdpClient client; 
        Thread receiveThread;
        IPEndPoint serverEP;
        int myId = -1;
        public GameObject localCube;
        public GameObject remoteCube; // o jogador remoto
        public GameObject remoteBall; // a bolinha
        Vector3 remoteBallPos;
        Vector3 remotePosition = Vector3.zero;

        void Start()
        {
            client = new UdpClient(); 
            serverEP = new IPEndPoint(IPAddress.Parse("10.57.1.35"), 5001);
            client.Connect(serverEP); 
            
            // Thread para ouvir respostas do servidor
            receiveThread = new Thread(ReceiveData);
            receiveThread.Start();

            // Envia mensagem inicial para o servidor
            byte[] hello = Encoding.UTF8.GetBytes("HELLO");
            client.Send(hello, hello.Length); 
        }
        void Update()
        { 
            // Movimenta o cubo local
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            localCube.transform.Translate(new Vector3(h, v, 0) * Time.deltaTime * 5); 
            
            // Envia posição formatada
            string msg = "POS:" + localCube.transform.position.x.ToString("F2", CultureInfo.InvariantCulture) + ";" + localCube.transform.position.y.ToString("F2", CultureInfo.InvariantCulture);
            byte[] data = Encoding.UTF8.GetBytes(msg);
            client.Send(data, data.Length);
            
            remoteCube.transform.position = Vector3.Lerp(remoteCube.transform.position, remotePosition, Time.deltaTime * 10f);
            remoteBall.transform.position = Vector3.Lerp(remoteBall.transform.position, remoteBallPos, Time.deltaTime * 10f);

        }
        void ReceiveData()
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0); 
            while (true)
            {
                byte[] data = client.Receive(ref remoteEP);
                string msg = Encoding.UTF8.GetString(data);
        
                if (msg.StartsWith("ASSIGN:"))
                {
                    myId = int.Parse(msg.Substring(7));
                    Debug.Log("Meu ID: " + myId);
                }
                else if (msg.StartsWith("PLAYER:"))
                {
                    string[] parts = msg.Split(':');
                    int id = int.Parse(parts[1]);
                    if (id == myId) continue;

                    string[] coords = parts[2].Split(';');
                    float x = float.Parse(coords[0], CultureInfo.InvariantCulture);
                    float y = float.Parse(coords[1], CultureInfo.InvariantCulture);
                    remoteCube.transform.position = new Vector3(x, y, 0);
                }
                else if (msg.StartsWith("BALL:"))
                {
                    string[] coords = msg.Substring(5).Split(';');
                    float x = float.Parse(coords[0], CultureInfo.InvariantCulture);
                    float y = float.Parse(coords[1], CultureInfo.InvariantCulture);
                    remoteBallPos = new Vector3(x, y, 0);
                }
            }
        
        }
        void OnApplicationQuit()
        {
            receiveThread.Abort(); 
            client.Close(); 
        } 
    }