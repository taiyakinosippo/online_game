using UnityEngine;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading;


public class TransportUDP : MonoBehaviour {

 //
 // ソケットによる送受信関連変数.
 //

 // クライアントとの送受信用ソケット.
 private Socket   m_socket = null;

 // 送信バッファ.
 private PacketQueue  m_sendQueue;
 
 // 受信バッファ.
 private PacketQueue  m_recvQueue;
 
 // サーバーフラグ. 
 private bool    m_isServer = false;

 // 接続フラグ.
 private bool   m_isConnected = false;

 //
 // イベント関連のメンバ変数.
 //

 // イベント通知のデリゲート.
 public delegate void  EventHandler(NetEventState state);

 private EventHandler m_handler;

 //
 // スレッド関連のメンバ変数.
 //

 // スレッド実行フラグ.
 protected bool   m_threadLoop = false;
 
 protected Thread  m_thread = null;

 // バッファのサイズはMTUの設定によって決まります.(MTU:1回に送信できる最大のデータサイズ)
 // イーサネットの最大MTUは1500bytesです.
 // この値はOSや端末などで異なるものですのでバッファのサイズは
 // 動作させる環境のMTUを調べて設定しましょう.
 private static int   s_mtu = 1400;


 // Use this for initialization
 void Start ()
    {
        // 送受信バッファを作成します.
        m_sendQueue = new PacketQueue();
        m_recvQueue = new PacketQueue(); 
 }
 
 // Update is called once per frame
 void Update ()
    {
 }

 // 待ち受け開始.
 public bool StartServer(int port, int connectionNum)
 {
        Debug.Log("StartServer called.!");

        // 送受信用ソケットを生成します.
        try {
   // ソケットを生成します.
   m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
   // 使用するポート番号を割り当てます.
   m_socket.Bind(new IPEndPoint(IPAddress.Any, port));
        }
        catch {
   Debug.Log("StartServer fail");
            return false;
        }

        m_isServer = true;

        return LaunchThread();
    }

 // 待ち受け終了.
    public void StopServer()
    {
  m_threadLoop = false;
        if (m_thread != null) {
            m_thread.Join();
            m_thread = null;
        }

        Disconnect();

  if (m_socket != null) {
   m_socket.Close();
   m_socket = null;
        }

        m_isServer = false;

        Debug.Log("Server stopped.");
    }


    // 接続処理.
    public bool Connect(string address, int port)
    {
  Debug.Log("TransportUdp::Connect called.[Port:" + port + "]");

  if (m_socket != null) {
            return false;
        }

  bool ret = false;
        try {
   m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

   // ※UDPでも通信相手とConnect関数を呼び出して接続して通信することもできます.
   //   接続して通信する場合は送信時にIPアドレスを指定しないSend関数を使用して送信することもできます.
   //   ここではTransportTCPと同じ関数で動作させるためConnect関数を使用しています.
   //   IPアドレスとポート番号をこのクラスで管理することでConnect関数を使用しないで通信することもできます.   
            m_socket.Connect(address, port);
   ret = LaunchThread();
  }
        catch {
            m_socket = null;
        }

  if (ret == true) {
   m_isConnected = true;
   Debug.Log("TransportUdp::Connect success.");
  }
  else {
   m_isConnected = false;
   Debug.Log("TransportUdp::Connect fail");
  }

        if (m_handler != null) {
            // 接続結果を通知します.
         // ゲームアプリケーションは他のプレイヤーが入室したときにユーザーへ通知するのがよいでしょう.
         // そのため入室したことをアプリケーションがわかるようにアプリケーション側の関数を呼び出すようにします.
   NetEventState state = new NetEventState();
   state.type = NetEventType.Connect;
   state.result = (m_isConnected == true) ? NetEventResult.Success : NetEventResult.Failure;
            m_handler(state);
   Debug.Log("event handler called");
        }

        return m_isConnected;
    }

 // 切断処理.
 public void Disconnect() {
        m_isConnected = false;

        if (m_socket != null) {
            // ソケットをクローズします.
   try {
             m_socket.Shutdown(SocketShutdown.Both);
             m_socket.Close();
             m_socket = null;
   }
   catch (SocketException e) {
    Debug.Log(e.Message);
   }
        }

        // 切断を通知します.
        // ゲームアプリケーションは他のプレイヤーが切断したときにユーザーに通知するのがよいでしょう.
        // そのため切断したことをアプリケーションがわかるようにアプリケーション側の関数を呼び出すようにします.
        if (m_handler != null) {
   NetEventState state = new NetEventState();
   state.type = NetEventType.Disconnect;
   state.result = NetEventResult.Success;
   m_handler(state);
        }
    }

    // 送信処理.
    public int Send(byte[] data, int size)
 {
  if (m_sendQueue == null) {
   return 0;
  }

  // 送信データは一旦キューにバッファリングするだけで送信はしていません.
  // 実際の送信は通信スレッド側(DispatchSend() 関数)で行います.
  // ゲームスレッド側の処理をできるだけ軽くするために直接 Send() 関数で送信していません.
  return m_sendQueue.Enqueue(data, size);
    }

    // 受信処理.
    public int Receive(ref byte[] buffer, int size)
 {
  if (m_recvQueue == null) {
   return 0;
  }

  // 実際の受信は通信スレッド側(DispatchReceive() 関数)で行います.
  // ゲームスレッド側の処理をできるだけ軽くするために直接 Receive() 関数で受信していません.
  return m_recvQueue.Dequeue(ref buffer, size);
    }

 // イベント通知関数登録.
    public void RegisterEventHandler(EventHandler handler)
    {
        m_handler += handler;
    }

 // イベント通知関数削除.
    public void UnregisterEventHandler(EventHandler handler)
    {
        m_handler -= handler;
    }

 // スレッド起動関数.
 bool LaunchThread()
 {
  try {
   // Dispatch用のスレッド起動.
   m_threadLoop = true;
   m_thread = new Thread(new ThreadStart(Dispatch));
   m_thread.Start();
  }
  catch {
   Debug.Log("Cannot launch thread.");
   return false;
  }
  
  return true;
 }

 // 通信スレッド側の送受信処理.
    public void Dispatch()
 {
  Debug.Log("Dispatch thread started.");

  while (m_threadLoop) {
   // クライアントからの接続を待ちます.
   AcceptClient();

   // クライアントとの送受信を処理します.
   if (m_socket != null && m_isConnected == true) {

             // 送信処理.
             DispatchSend();

             // 受信処理.
             DispatchReceive();
         }

   Thread.Sleep(5);
  }

  Debug.Log("Dispatch thread ended.");
    }

 // 通信相手の待ち受け.
 void AcceptClient()
 {
  if (m_isConnected == false &&
      m_socket != null && 
      m_socket.Poll(0, SelectMode.SelectRead)) {
   // クライアントから接続されました.
   m_isConnected = true;

   // 接続を通知します.
         // ゲームなどのアプリケーションは他のプレイヤーが入室したときにユーザーに通知するのがよいでしょう.
         // そのため入室したことをアプリケーションがわかるようにアプリケーション側の関数を呼び出すようにします.
   if (m_handler != null) {
    NetEventState state = new NetEventState();
    state.type = NetEventType.Connect;
    state.result = NetEventResult.Success;
    m_handler(state);
   }
  }
 }

 // スレッド側の送信処理.
    void DispatchSend()
 {
        try {
            // 送信処理.
            if (m_socket.Poll(0, SelectMode.SelectWrite)) {
    byte[] buffer = new byte[s_mtu];

    // Send関数でバッファリングされたデータを取り出して送信を行います.
                int sendSize = m_sendQueue.Dequeue(ref buffer, buffer.Length);
                // 送信データがなくなるまで送信を続けます.
                while (sendSize > 0) {
                    m_socket.Send(buffer, sendSize, SocketFlags.None);
                    sendSize = m_sendQueue.Dequeue(ref buffer, buffer.Length);
                }
            }
        }
        catch {
            return;
        }
    }

 // スレッド側の受信処理.
    void DispatchReceive()
 {
        // 受信処理.
        try {
            while (m_socket.Poll(0, SelectMode.SelectRead)) {
    byte[] buffer = new byte[s_mtu];

                int recvSize = m_socket.Receive(buffer, buffer.Length, SocketFlags.None);
                // 通信相手と切断したことにReceive関数の関数値は0が返されます.
                if (recvSize == 0) {
                    // 切断.
                    Debug.Log("Disconnect recv from client.");
                    Disconnect();
                }
                else if (recvSize > 0) {
                 // ゲームスレッド側に受信したデータを渡すために受信データをキューに追加します.
                    m_recvQueue.Enqueue(buffer, recvSize);
                }
            }
        }
        catch {
            return;
        }
    }

 // サーバー動作設定確認.
 public bool IsServer() {
  return m_isServer;
 }
 
    // 接続確認.
    public bool IsConnected() {
        return m_isConnected;
    }

}
 