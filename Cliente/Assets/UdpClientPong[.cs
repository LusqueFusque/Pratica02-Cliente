using System.Collections.Concurrent; // para ConcurrentQueue
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Globalization;

public class UdpClientPong : MonoBehaviour
{
    UdpClient client;
    Thread receiveThread;
    IPEndPoint serverEP;

    public GameObject localCube;
    public GameObject remoteCube;
    public GameObject ball;

    int myId = -1;

    // Variáveis thread-safe para armazenar posições recebidas
    private ConcurrentQueue<Vector3> remotePositionsQueue = new ConcurrentQueue<Vector3>();
    private ConcurrentQueue<Vector3> ballPositionsQueue = new ConcurrentQueue<Vector3>();

    void Start()
    {
        client = new UdpClient();
        serverEP = new IPEndPoint(IPAddress.Parse("10.57.1.35"), 5001);
        client.Connect(serverEP);

        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true; // garante que thread será finalizada quando fechar app
        receiveThread.Start();

        byte[] hello = Encoding.UTF8.GetBytes("HELLO");
        client.Send(hello, hello.Length);
    }

    void Update()
    {
        // Movimento do cubo local
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        localCube.transform.Translate(new Vector3(h, v, 0) * Time.deltaTime * 5);

        // Envia posição para o servidor
        string msg = "POS:" + localCube.transform.position.x.ToString("F2", CultureInfo.InvariantCulture) + ";" + localCube.transform.position.y.ToString("F2", CultureInfo.InvariantCulture);
        byte[] data = Encoding.UTF8.GetBytes(msg);
        client.Send(data, data.Length);

        // Aplica posição recebida para remoteCube (se houver)
        if (remotePositionsQueue.TryDequeue(out Vector3 remotePos))
        {
            remoteCube.transform.position = Vector3.Lerp(remoteCube.transform.position, remotePos, Time.deltaTime * 10f);
        }

        // Aplica posição recebida para bola (se houver)
        if (ballPositionsQueue.TryDequeue(out Vector3 ballPos))
        {
            ball.transform.position = Vector3.Lerp(ball.transform.position, ballPos, Time.deltaTime * 10f);
        }
    }

    void ReceiveData()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

        while (true)
        {
            try
            {
                byte[] data = client.Receive(ref remoteEP);
                string msg = Encoding.UTF8.GetString(data);

                if (msg.StartsWith("ASSIGN:"))
                {
                    myId = int.Parse(msg.Substring(7));
                    Debug.Log("[Cliente] Recebi ID = " + myId);
                }
                else if (msg.StartsWith("PLAYER:"))
                {
                    string[] parts = msg.Split(':');
                    int id = int.Parse(parts[1]);
                    if (id == myId) continue;

                    string[] coords = parts[2].Split(';');
                    float x = float.Parse(coords[0], CultureInfo.InvariantCulture);
                    float y = float.Parse(coords[1], CultureInfo.InvariantCulture);

                    remotePositionsQueue.Enqueue(new Vector3(x, y, 0));
                }
                else if (msg.StartsWith("BALL:"))
                {
                    string[] coords = msg.Substring(5).Split(';');
                    float x = float.Parse(coords[0], CultureInfo.InvariantCulture);
                    float y = float.Parse(coords[1], CultureInfo.InvariantCulture);

                    ballPositionsQueue.Enqueue(new Vector3(x, y, 0));
                }
            }
            catch (SocketException ex)
            {
                Debug.LogWarning("Socket encerrado ou erro: " + ex.Message);
                break;
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Erro no cliente: " + ex.Message);
            }
        }
    }

    void OnApplicationQuit()
    {
        receiveThread?.Abort();
        client?.Close();
    }
}
