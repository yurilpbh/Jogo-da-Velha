using System;
using System.Net.Sockets;
using System.Net;
using ClienteHandler;

namespace Servidor
{
    public class Program
    {
        
        private const int PORT = 10000;
        private static TcpListener listener;
        static void Main(string[] args)
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, PORT);
            listener = new TcpListener(endPoint);

            //Inicia o servidor
            listener.Start();
            Console.WriteLine("Servidor pronto");
            int clientCounter = 0;

            while (true)
            {
                //Aceita ligações
                TcpClient client = listener.AcceptTcpClient();
                clientCounter++;
                //Utiliza threads para tratar dos clientes
                ClientHandler clientHandler = new ClientHandler(client, clientCounter);
                clientHandler.Handle();
            }
        }
    }
}
