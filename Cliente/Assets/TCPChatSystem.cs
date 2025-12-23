using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;
using TMPro;
using UnityEngine.UI;
using System.Threading;
using System.IO;

public class TCPChatSystem : MonoBehaviour
{
    [Header("Configurações de Rede")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 5556;
    
    [Header("UI do Chat")]
    public TMP_InputField chatInput;
    public TextMeshProUGUI chatDisplay;
    public ScrollRect scrollRect;
    public Button sendButton;
    public int maxMessages = 50;
    
    [Header("Configurações")]
    public string playerName = "Jogador";
    
    // Rede - Cliente TCP
    private TcpClient tcpClient;
    private NetworkStream stream;
    private StreamReader reader;
    private Thread receiveThread;
    private volatile bool isRunning = false;
    
    // UI
    private System.Collections.Generic.List<string> chatMessages = new System.Collections.Generic.List<string>();
    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
    
    void Start()
    {
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(SendChatMessage);
        }
        
        if (chatDisplay != null)
        {
            chatDisplay.text = "";
        }
        
        ConnectToServer();
    }
    
    void ConnectToServer()
    {
        try
        {
            tcpClient = new TcpClient();
            tcpClient.Connect(serverIP, serverPort);
            stream = tcpClient.GetStream();
            reader = new StreamReader(stream, Encoding.UTF8);
            
            isRunning = true;
            
            // Envia mensagem de conexão
            SendToServer($"CONNECT|{playerName}");
            
            // Inicia thread de recepção
            receiveThread = new Thread(ReceiveMessages);
            receiveThread.IsBackground = true;
            receiveThread.Start();
            
            AddSystemMessage($"Conectado ao chat");
            Debug.Log($"[Chat] Conectado ao servidor {serverIP}:{serverPort}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Chat] Erro ao conectar: {e.Message}");
            AddSystemMessage("Erro ao conectar ao chat");
        }
    }
    
    void ReceiveMessages()
    {
        while (isRunning && tcpClient != null && tcpClient.Connected)
        {
            try
            {
                if (stream.DataAvailable)
                {
                    string message = reader.ReadLine();
                    
                    if (!string.IsNullOrEmpty(message))
                    {
                        messageQueue.Enqueue(message);
                    }
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
            catch (System.Exception e)
            {
                if (isRunning)
                {
                    Debug.Log($"[Chat] Desconectado: {e.Message}");
                    messageQueue.Enqueue("SYSTEM|Desconectado do servidor");
                }
                break;
            }
        }
    }
    
    void Update()
    {
        // Processa mensagens recebidas
        while (messageQueue.TryDequeue(out string message))
        {
            ProcessMessage(message);
        }
        
        // Enter para enviar
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (chatInput != null && chatInput.isFocused)
            {
                SendChatMessage();
            }
        }
    }
    
    void ProcessMessage(string message)
    {
        string[] parts = message.Split('|');
        if (parts.Length < 2) return;
        
        if (parts[0] == "CHAT" && parts.Length >= 3)
        {
            AddChatMessage(parts[1], parts[2]);
        }
        else if (parts[0] == "SYSTEM" && parts.Length >= 2)
        {
            AddSystemMessage(parts[1]);
        }
    }
    
    public void SendChatMessage()
    {
        if (chatInput == null) return;
        
        string message = chatInput.text.Trim();
        
        if (string.IsNullOrEmpty(message)) return;
        
        // Adiciona localmente (visualização imediata)
        AddChatMessage(playerName, message);
        
        // Envia para servidor redistribuir
        SendToServer($"CHAT|{playerName}|{message}");
        
        chatInput.text = "";
        chatInput.ActivateInputField();
    }
    
    void SendToServer(string message)
    {
        if (stream == null || !stream.CanWrite) return;
        
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message + "\n");
            stream.Write(data, 0, data.Length);
            stream.Flush();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Chat] Erro ao enviar: {e.Message}");
        }
    }
    
    void AddChatMessage(string sender, string message)
    {
        string formatted = $"<color=#00FFFF>{sender}:</color> {message}";
        AddMessageToDisplay(formatted);
    }
    
    void AddSystemMessage(string message)
    {
        string formatted = $"<color=#FFFF00>[Sistema]</color> {message}";
        AddMessageToDisplay(formatted);
    }
    
    void AddMessageToDisplay(string message)
    {
        chatMessages.Add(message);
        
        if (chatMessages.Count > maxMessages)
        {
            chatMessages.RemoveAt(0);
        }
        
        UpdateChatDisplay();
    }
    
    void UpdateChatDisplay()
    {
        if (chatDisplay != null)
        {
            chatDisplay.text = string.Join("\n", chatMessages.ToArray());
            
            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }
    
    void OnApplicationQuit()
    {
        Disconnect();
    }
    
    void OnDestroy()
    {
        Disconnect();
    }
    
    void Disconnect()
    {
        if (!isRunning) return;
        
        isRunning = false;
        
        try
        {
            SendToServer($"DISCONNECT|{playerName}");
        }
        catch { }
        
        try
        {
            reader?.Close();
            stream?.Close();
            tcpClient?.Close();
        }
        catch { }
        
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(500);
        }
        
        Debug.Log("[Chat] Desconectado");
    }
}