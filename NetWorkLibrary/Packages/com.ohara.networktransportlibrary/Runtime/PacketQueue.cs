using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

public class PacketQueue
{
    // パケット格納情報.
    struct PacketInfo
    {
        public int offset;
        public int size;
    };

    //
    private MemoryStream m_streamBuffer;

    private List<PacketInfo> m_offsetList;

    private int m_offset = 0;


    private Object lockObj = new Object();

    //  コンストラクタ(ここで初期化を行います).
    public PacketQueue()
    {
        m_streamBuffer = new MemoryStream();
        m_offsetList = new List<PacketInfo>();
    }

    // キューを追加.(エンキュー)
    public int Enqueue(byte[] data, int size)
    {
        PacketInfo info = new PacketInfo();

        info.offset = m_offset;
        info.size = size;

        lock (lockObj)
        {
            // パケット格納情報を保存します.
            m_offsetList.Add(info);

            // パケットデータを保存します.
            m_streamBuffer.Position = m_offset;
            m_streamBuffer.Write(data, 0, size);
            m_streamBuffer.Flush();
            m_offset += size;
        }

        return size;
    }

    // キューの取り出し.(デキュー)
    public int Dequeue(ref byte[] buffer, int size)
    {

        if (m_offsetList.Count <= 0)
        {
            return -1;
        }

        int recvSize = 0;
        lock (lockObj)
        {
            PacketInfo info = m_offsetList[0];

            // バッファから該当するパケットデータを取得します.
            int dataSize = Math.Min(size, info.size);
            m_streamBuffer.Position = info.offset;
            recvSize = m_streamBuffer.Read(buffer, 0, dataSize);

            // キューデータを取り出したので先頭要素を削除します.
            if (recvSize > 0)
            {
                m_offsetList.RemoveAt(0);
            }

            // すべてのキューデータを取り出したときはストリームをクリアしてメモリを節約します.
            if (m_offsetList.Count == 0)
            {
                Clear();
                m_offset = 0;
            }
        }

        return recvSize;
    }

    // キューをクリア. 
    public void Clear()
    {
        byte[] buffer = m_streamBuffer.GetBuffer();
        Array.Clear(buffer, 0, buffer.Length);

        m_streamBuffer.Position = 0;
        m_streamBuffer.SetLength(0);
    }
}
 