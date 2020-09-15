using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Security.Cryptography;
using EI.SI;
using System.Net.Http;
using System.Threading;
using System.IO;
using System.Dynamic;
using System.Security.Principal;
using System.Data.SqlClient;
using System.Net.Mail;

namespace Servidor
{
    class Program
    {
        private static Mutex connection = new Mutex(); //Mutex utilizado para fazer broadcast para os clientes
        private const int PORT = 10000;
        private static TcpListener listener;
        private static List<Room> rooms = new List<Room>();
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

        //Classe que trata dos elementos da sala do jogo
        class Room
        {
            private string name, jogador1, jogador2;
            private int pontosJog1, pontosJog2, pontosEmp;
            private List<string> proximoJogador = new List<string>();
            private List<ClientHandler> clientsList = new List<ClientHandler>();
            public Jogo jogo;
            private Boolean allowMultiplePlayers = false;

            public Room(string name)
            {
                this.name = name;
                jogo = new Jogo();
                pontosJog1 = 0;
                pontosJog2 = 0;
                pontosEmp = 0;
            }

            public void addClientToRoom(ClientHandler client)
            {
                if (!clientsList.Contains(client))
                {
                    clientsList.Add(client);
                }
            }

            public void removeClientOfRoom(ClientHandler client)
            {
                if (clientsList.Contains(client))
                {
                    clientsList.Remove(client);
                }
            }

            public string getName()
            {
                return name;
            }

            public void setJogador(string nome)
            {
                if(jogador1 == null)
                {
                    jogador1 = nome;
                }
                else if (jogador2 == null)
                {
                    jogador2 = nome;
                }
                else
                {
                    proximoJogador.Add(nome);
                }
            }
            public string getNomeJogador(int pos)
            {
                if (pos == 1)
                {
                    return jogador1;
                }
                else if (pos == 2)
                {
                    return jogador2;
                }
                else
                {
                    return proximoJogador.ElementAt(3-pos);
                }
            }

            public int getPosJogador(string nome)
            {
                if(nome == jogador1)
                {
                    return 1;
                }
                else if(nome == jogador2)
                {
                    return 2;
                }
                else
                {
                    return 3 + proximoJogador.IndexOf(nome);
                }
            }

            public void trocaJogadores()
            {
                string aux = jogador1;
                jogador1 = jogador2;
                jogador2 = aux;
            }

            public Boolean isFull()
            {
                if (!allowMultiplePlayers)
                {
                    if(clientsList.Count() == 2)
                    {
                        return true;
                    }
                }
                else if(clientsList.Count() == 10)
                {
                    return true;
                }
                return false;
            }

            public void multiplePlayers ()
            {
                if (allowMultiplePlayers) { return; }
                allowMultiplePlayers = true;
            }

            public List<ClientHandler> getClientList()
            {
                return clientsList;
            }

            public void writeLog(string msg) //Escreve o log do chat em um arquivo .txt
            {
                string path = @"logRoom-" + this.name + ".txt";
                File.AppendAllText(path, msg + Environment.NewLine, Encoding.UTF8);
            }

            public int move(int line, int col, string nome)
            {
                int posJogador = nome == jogador1 ? 0 : 1;
                return jogo.move(line, col, posJogador);
            }

            public void novoJogo()
            {
                jogo = new Jogo();
            }

            public void ganhador(int quem)
            {
                switch (quem)
                {
                    case 1:
                        pontosJog1++;
                        break;
                    case 2:
                        pontosJog2++;
                        break;
                    case 3:
                        pontosEmp++;
                        break;
                }
                
            }

            public int getPontos(int quais)
            {
                switch (quais)
                {
                    case 1:
                        return pontosJog1;
                    case 2:
                        return pontosJog2;
                    case 3:
                        return pontosEmp;
                    default:
                        return 0;
                }
            }
        }

        class Jogo
        {
            private char[,] matrix;
            private int moves;
            private char ultimoJogador='O';
            public Jogo()
            {
                this.matrix = new char[3, 3] { {' ', ' ', ' '}, {' ', ' ', ' '}, {' ', ' ', ' '} };
                this.moves = 0;
            }

            private Boolean isWinner(int playerSymbol) //Verifica se a jogada atual levou o jogador à vitória
            {
                for (int i = 0; i < 3; i++)
                {
                    int colCount = 0;
                    for (int j = 0; j < 3; j++)
                    {
                        if(matrix[i,j] == playerSymbol)
                        {
                            colCount++;
                        }
                    }
                    if (colCount == 3)
                    {
                        return true;
                    }
                }
                for (int i = 0; i < 3; i++)
                {
                    int lineCount = 0;
                    for (int j = 0; j < 3; j++)
                    {
                        if (matrix[j, i] == playerSymbol)
                        {
                            lineCount++;
                        }
                    }
                    if (lineCount == 3)
                    {
                        return true;
                    }
                }
                if ( (matrix[0, 0] == playerSymbol && matrix[1, 1] == playerSymbol && matrix[2, 2] == playerSymbol) ||
                     (matrix[0, 2] == playerSymbol && matrix[1, 1] == playerSymbol && matrix[2, 0] == playerSymbol) )
                {
                    return true;
                }
                return false;
            }

            //Faz a jogada executada pelo jogador e verifica se o movimento é válido, se houve empate ou se houve vitória
            public int move(int line, int col, int player) 
            {
                char playerSymbol = player == 0 ? 'X' : 'O';
                if(playerSymbol == ultimoJogador)
                {
                    return 3;
                }
                ultimoJogador = playerSymbol;
                if (matrix[line, col] == ' ' && line < 3 && line >= 0 && col < 3 && col >= 0)
                {
                    matrix[line, col] = playerSymbol;
                    moves++;
                }
                else
                {
                    Console.WriteLine("Movimento inválido");
                    return -1;
                }
                if (moves > 4)
                {
                    if (isWinner(playerSymbol))
                    {
                        return 1;
                    }
                }
                if (moves == 9)
                {
                    return 2;
                }
                return 0;
            }

            public Boolean jogoComecou()
            {
                return moves == 0 ? false : true;
            }
        }

        //Thread que trata dos elementos dos clientes
        class ClientHandler
        {
            private TcpClient tcpClient;
            private NetworkStream networkStream;
            private ProtocolSI protocolSI;
            private Room room;
            private Security security;
            private int clientID;
            private string nomeJogador;
            private byte[] simetricKey;
            private byte[] IV;
            

            public ClientHandler(TcpClient client, int clientID)
            {
                tcpClient = client;
                this.clientID = clientID;
                security = new Security();
            }

            public void Handle()
            {
                Thread thread = new Thread(threadHandler);
                thread.Start();
            }

            public byte[] getSimetricKey()
            {
                return simetricKey;
            }

            public void enviaACK() //Envia o ACK para o cliente
            {
                byte[] ack = protocolSI.Make(ProtocolSICmdType.ACK);
                networkStream.Write(ack, 0, ack.Length);
            }

            public bool esperaACK() //Espera o ACK do cliente
            {
                networkStream.ReadTimeout = 100;
                try
                {
                    networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                    while (protocolSI.GetCmdType() != ProtocolSICmdType.ACK)
                    {
                        networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                    }
                    networkStream.ReadTimeout = -1;
                    return true;
                }
                catch (IOException)
                {
                    networkStream.ReadTimeout = -1;
                    return false;
                }

            }

            public void broadcast(string msg, ProtocolSICmdType cmd) //Faz um broadcast para todos os jogadores
            {
                connection.WaitOne(); //Adquire controle do networkStream
                byte[] msgToSend;
                NetworkStream networkStream = tcpClient.GetStream();
                foreach (ClientHandler client in room.getClientList()) 
                {
                    NetworkStream newNetworkStream = client.tcpClient.GetStream(); //Cria uma nova via de comunicação para o client
                    msgToSend = protocolSI.Make(cmd, client.security.CifrarTexto(msg));
                    newNetworkStream.Write(msgToSend, 0, msgToSend.Length);
                    esperaACK();
                }
                connection.ReleaseMutex(); //Libera o networkStream
            }

            private Boolean trocaDePosicao(Boolean solicitou)
            {
                if (solicitou)
                {
                    if(room.getClientList().Count == 1)
                    {
                        room.trocaJogadores();
                        string msg = String.Format("1/Agora você é o jogador {0}", room.getPosJogador(nomeJogador));
                        byte[] msgByte = protocolSI.Make(ProtocolSICmdType.USER_OPTION_7, security.CifrarTexto(msg));
                        networkStream.Write(msgByte, 0, msgByte.Length);
                        esperaACK();
                        return false;
                    }
                    string outroJogador = room.getNomeJogador(room.getPosJogador(nomeJogador) == 1 ? 2 : 1);
                    foreach (ClientHandler client in this.room.getClientList())
                    {
                        if (client.nomeJogador == outroJogador)
                        {
                            string msg = String.Format("O jogador {0} solicitou trocar de posição, você aceita?", this.nomeJogador);
                            byte[] msgByte = protocolSI.Make(ProtocolSICmdType.USER_OPTION_7, security.CifrarTexto(msg));

                            connection.WaitOne(); //Adquire controle único do networkStream para fazer o broadcast
                            NetworkStream newNetworkStream = client.tcpClient.GetStream(); //Cria uma nova via de comunicação para aquele client
                            newNetworkStream.Write(msgByte, 0, msgByte.Length);
                            newNetworkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                            while (protocolSI.GetCmdType() != ProtocolSICmdType.USER_OPTION_1 || protocolSI.GetCmdType() != ProtocolSICmdType.USER_OPTION_2)
                            {
                                newNetworkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                            }
                            newNetworkStream.Close();
                            connection.ReleaseMutex(); //Libera o networkStream
                            if (protocolSI.GetCmdType() == ProtocolSICmdType.USER_OPTION_1)
                            {
                                msg = "1/Solicitação aceita";
                                msgByte = protocolSI.Make(ProtocolSICmdType.USER_OPTION_7, security.CifrarTexto(msg));
                                networkStream.Write(msgByte, 0, msgByte.Length);
                                esperaACK();
                                room.trocaJogadores();
                            } else
                            {
                                msg = "0/Solicitação negada";
                                msgByte = protocolSI.Make(ProtocolSICmdType.USER_OPTION_7, security.CifrarTexto(msg));
                                networkStream.Write(msgByte, 0, msgByte.Length);
                                esperaACK();
                            }
                            break;
                        }
                    }
                }
                return false;
            }

            private void threadHandler() //Trata as mensagens que chegam e que são enviadas
            {
                networkStream = this.tcpClient.GetStream();
                protocolSI = new ProtocolSI();
                Boolean trocaPosicao = false;
                while (protocolSI.GetCmdType() != ProtocolSICmdType.EOT) //Enquanto a thread não receber ordens para terminar
                {
                    networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                    ProtocolSICmdType cmd = protocolSI.GetCmdType();
                    string msg;
                    byte[] msgByte;
                    switch (protocolSI.GetCmdType())
                    {
                        case ProtocolSICmdType.PUBLIC_KEY:
                            security.setPublicKey(protocolSI.GetStringFromData());
                            Console.WriteLine("Recebi uma chave pública");
                            enviaACK();

                            simetricKey = protocolSI.Make(ProtocolSICmdType.SYM_CIPHER_DATA, security.getSimetricKey());
                            networkStream.Write(simetricKey, 0, simetricKey.Length);
                            esperaACK();

                            IV = protocolSI.Make(ProtocolSICmdType.IV, security.getIV());
                            networkStream.Write(IV, 0, IV.Length);
                            esperaACK();
                            break;

                        case ProtocolSICmdType.USER_OPTION_1: //Adquire o nome do jogador
                            connection.WaitOne();       //Caso no qual é feito um broadcast e a thread "errada" recebe o ACK e, portanto
                            connection.ReleaseMutex();  //espera até que a thread "correta" receba o ACK para poder voltar a esperar nova mensagem
                            nomeJogador = security.DecifrarTexto(protocolSI.GetStringFromData());
                            Console.WriteLine("Jogador {0} - {1}, conectou-se", clientID, nomeJogador);
                            enviaACK();
                            break;

                        case ProtocolSICmdType.USER_OPTION_2: //Atualiza os jogadores presentes na sala
                            string salaDesejada = security.DecifrarTexto(protocolSI.GetStringFromData());
                            byte[] newJogador;

                            foreach (Room sala in rooms) //Percorre a lista de salas verificando se a sala na qual o cliente deseja conectar-se já existe
                            {
                                if (sala.getName() == salaDesejada)
                                {
                                    if(!sala.isFull()) //Verifica se a sala não está cheia
                                    {
                                        sala.addClientToRoom(this);
                                        this.room = sala;
                                        break;
                                    }
                                    else
                                    {
                                        goto SalaCheia;
                                    }
                                }
                            }

                            if (room == null) //Cria a sala caso a mesma não exista
                            {
                                this.room = new Room(salaDesejada);
                                rooms.Add(this.room);
                                room.addClientToRoom(this);
                                msg = System.DateTime.Now.ToString();
                                room.writeLog(msg);
                            }

                            Console.WriteLine("{0} entrou na sala {1}", nomeJogador, salaDesejada);
                            enviaACK();

                            if (room.getClientList().Count == 1) //Se aquele jogador é o único na sala
                            {
                                //Coloca o jogador como o jogador 1
                                room.setJogador(nomeJogador);
                                msg = String.Format("1/{0}", nomeJogador);
                                newJogador = protocolSI.Make(ProtocolSICmdType.USER_OPTION_1, security.CifrarTexto(msg));
                                networkStream.Write(newJogador, 0, newJogador.Length);
                                esperaACK();
                            }
                            else if (room.getClientList().Count > 2) //Se tem ao menos 2 jogadores na sala
                            {
                                //Coloca os próximos jogadores na fila
                                room.setJogador(nomeJogador);
                                msg = String.Format("3/{0}/{1}/{2}/{3}/{4}/{5}", room.getNomeJogador(1), room.getNomeJogador(2), room.getPontos(1), room.getPontos(2), room.getPontos(3), this.nomeJogador);
                                newJogador = protocolSI.Make(ProtocolSICmdType.USER_OPTION_1, security.CifrarTexto(msg));
                                networkStream.Write(newJogador, 0, newJogador.Length);
                                esperaACK();
                            }
                            else //Se só há 1 jogador na sala
                            {
                                int posNovoJogador;
                                this.room.setJogador(this.nomeJogador);
                                foreach (ClientHandler client in this.room.getClientList())
                                {
                                    if (client.clientID != this.clientID)
                                    {
                                        posNovoJogador = room.getNomeJogador(2) == this.nomeJogador ? 2 : 1; //Descobre qual será a posição do novo jogador
                                        msg = String.Format("{0}/{1}", posNovoJogador, this.nomeJogador);
                                        newJogador = protocolSI.Make(ProtocolSICmdType.USER_OPTION_1, client.security.CifrarTexto(msg));

                                        connection.WaitOne(); //Adquire controle único do networkStream para fazer o broadcast
                                        NetworkStream newNetworkStream = client.tcpClient.GetStream(); //Cria uma nova via de comunicação para aquele client
                                        newNetworkStream.Write(newJogador, 0, newJogador.Length); //Envia o novo jogador para o jogador que já está na sala
                                        esperaACK();
                                        connection.ReleaseMutex(); //Libera o networkStream

                                        //Coloca-se na posição que resta
                                        networkStream.Write(newJogador, 0, newJogador.Length);
                                        esperaACK();
                                    }
                                    else
                                    {
                                        //Envia o nome do jogador que já está na sala para o novo jogador
                                        int posJogadorPresente = room.getNomeJogador(1) != this.nomeJogador ? 1 : 2;
                                        msg = String.Format("{0}/{1}", posJogadorPresente, room.getNomeJogador(posJogadorPresente));
                                        msgByte = protocolSI.Make(ProtocolSICmdType.USER_OPTION_1, security.CifrarTexto(msg));
                                        networkStream.Write(msgByte, 0, msgByte.Length);
                                        esperaACK();
                                    }
                                }
                                //Broadcast que informa que há 2 jogadores na sala e, portanto o jogo pode iniciar
                                broadcast(" ", ProtocolSICmdType.USER_OPTION_3);
                            }
                            break;
                        SalaCheia:
                            msgByte = protocolSI.Make(ProtocolSICmdType.USER_OPTION_3);
                            networkStream.Write(msgByte, 0, msgByte.Length);
                            esperaACK();
                            break;

                        case ProtocolSICmdType.DATA: //Transmite o que o jogador disse para o chat
                            msg = $"{System.DateTime.Now.ToString("HH:mm:ss")} - {nomeJogador} : {security.DecifrarTexto(protocolSI.GetStringFromData())}";
                            Console.WriteLine(msg);
                            broadcast(msg, ProtocolSICmdType.DATA); //Broadcast da mensagem para todos os jogadores
                            room.writeLog(msg); //Escreve para o arquivo de texto as mensagens do chat
                            break;

                        case ProtocolSICmdType.USER_OPTION_3: //Trata da jogada executada utilizando assinaturas digitais
                            //Recebe o movimento cifrado
                            string move = security.DecifrarTexto(protocolSI.GetStringFromData());
                            //Espera pelo hash assinado do movimento cifrado com a chave privada
                            networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                            while (protocolSI.GetCmdType() != ProtocolSICmdType.USER_OPTION_4)
                            {
                                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                            }
                            string moveSign = security.DecifrarTexto(protocolSI.GetStringFromData());
                            //Verifica a autenticidade do movimento
                            if (security.verifySignData(move, moveSign))
                            {
                                string[] coordenadas = move.Split('/');
                                int line = int.Parse(coordenadas[0]);
                                int col = int.Parse(coordenadas[1]);
                                string symbolPlayer = room.getNomeJogador(1) == this.nomeJogador ? "X" : "O";
                                switch (room.move(line, col, this.nomeJogador))
                                {
                                    case -1: //Movimento é inválido
                                        msg = "Movimento inválido, tente novamente!";
                                        byte[] invalid = protocolSI.Make(ProtocolSICmdType.USER_OPTION_4, security.CifrarTexto(msg));
                                        networkStream.Write(invalid, 0, invalid.Length);
                                        break;

                                    case 0: //Movimento válido
                                        broadcast(String.Format("{0}{1}/{2}", line, col, symbolPlayer), ProtocolSICmdType.USER_OPTION_5);
                                        break;

                                    case 1: //Jogo termina com um ganhador
                                        broadcast(String.Format("{0}{1}/{2}", line, col, symbolPlayer), ProtocolSICmdType.USER_OPTION_5);
                                        broadcast(String.Format("{0} ganhou!", nomeJogador), ProtocolSICmdType.USER_OPTION_6);
                                        room.novoJogo();
                                        trocaPosicao = trocaDePosicao(trocaPosicao);
                                        break;

                                    case 2://Jogo termina em empate
                                        broadcast(String.Format("{0}{1}/{2}", line, col, symbolPlayer), ProtocolSICmdType.USER_OPTION_5);
                                        broadcast(String.Format("Empate!", nomeJogador), ProtocolSICmdType.USER_OPTION_6);
                                        room.novoJogo();
                                        trocaPosicao = trocaDePosicao(trocaPosicao);
                                        break;

                                    case 3: //Jogador incorreto tentou fazer o movimento
                                        msg = "Espere a sua vez!";
                                        byte[] jogadorIncorreto = protocolSI.Make(ProtocolSICmdType.USER_OPTION_4, security.CifrarTexto(msg));
                                        networkStream.Write(jogadorIncorreto, 0, jogadorIncorreto.Length);
                                        break;
                                }
                            }
                            else
                            {
                                Console.WriteLine("Mensagem enviada inválida");
                                msg = "Ocorreu algum erro, tente novamente!";
                                byte[] invalid = protocolSI.Make(ProtocolSICmdType.USER_OPTION_7, security.CifrarTexto(msg));
                                networkStream.Write(invalid, 0, invalid.Length);
                            }
                            break;

                        case ProtocolSICmdType.USER_OPTION_5: //Jogador solicitou troca de posição
                            trocaPosicao = true;
                            trocaDePosicao(!room.jogo.jogoComecou());
                            break;

                        case ProtocolSICmdType.USER_OPTION_6: //Jogador solicitou permitir vários jogadores
                            room.multiplePlayers();
                            msg = "Múltiplos jogadores habilitado";
                            broadcast(msg, ProtocolSICmdType.USER_OPTION_8);
                            break;

                        case ProtocolSICmdType.SECRET_KEY: //Recebe a senha do usuário
                            Console.WriteLine("Recebi a senha");
                            string senha = security.DecifrarTexto(protocolSI.GetStringFromData());
                            if (security.VerifyLogin(this.nomeJogador, senha)) { //Autentica o jogador
                                Console.WriteLine("{0} autenticado com sucesso", this.nomeJogador);
                                enviaACK();
                            }
                            else
                            {
                                Console.WriteLine("{0} senha incorreta", this.nomeJogador);
                                byte[] msgConnection = protocolSI.Make(ProtocolSICmdType.USER_OPTION_3);
                                networkStream.Write(msgConnection, 0, msgConnection.Length);
                                esperaACK();
                            }
                            break;

                        case ProtocolSICmdType.EOT: //Finaliza a sessão do jogador
                            Console.WriteLine("Ending Thread from {0}", nomeJogador);
                            enviaACK();
                            if(room != null && room.getClientList().Count == 2)
                            {
                                int jogadorPos = room.getNomeJogador(1) == nomeJogador ? 1 : 2;
                                connection.WaitOne(); //Adquire controle único do networkStream para fazer o broadcast
                                foreach (ClientHandler client in this.room.getClientList()) //Faz um broadcast para atualizar todos os jogadores
                                {
                                    if (client.clientID != this.clientID)
                                    {
                                        msg = String.Format("{0}/{1}", nomeJogador, jogadorPos);
                                        NetworkStream newNetworkStream = client.tcpClient.GetStream(); //Cria uma nova via de comunicação para aquele client
                                        msgByte = protocolSI.Make(ProtocolSICmdType.USER_OPTION_9, client.security.CifrarTexto(msg));
                                        newNetworkStream.Write(msgByte, 0, msgByte.Length);
                                        break;
                                    }
                                }
                                connection.ReleaseMutex(); //Libera o networkStream
                                room.novoJogo();
                            }
                            break;

                        case ProtocolSICmdType.ACK: //Caso no qual é feito um broadcast e a thread "errada" recebe o ACK e, portanto
                            connection.WaitOne();   //espera até que a thread "correta" receba o ACK para poder voltar a esperar nova mensagem
                            connection.ReleaseMutex();
                            break;

                        default:
                            break;
                    }
                }
                networkStream.Close();
                this.tcpClient.Close();
                if (room != null)
                {
                    this.room.removeClientOfRoom(this);
                    this.room = null;
                }
            }
        }

        class Security //Classe que trata de toda a segurança da conexão
        {
            private string publicKey;
            private AesCryptoServiceProvider aes;
            private RSACryptoServiceProvider rsa;
            private static int SALTSIZE;
            private static int NUMBER_OF_ITERATIONS;

            public Security(int saltSize = 8, int nOfIterations = 50000)
            {
                //Inicializa o serviço de cifragem aes, simétrico
                aes = new AesCryptoServiceProvider();
                //Guardar a chave simétrica
                aes.Key = Convert.FromBase64String(GerarChaveSimetrica("pass"));
                //Guardar o vetor de inicialização
                aes.IV = Convert.FromBase64String(GerarIV("pass"));
                //Inicializa o serviço de cifragem rsa, assimétrico
                rsa = new RSACryptoServiceProvider();
                SALTSIZE = saltSize;
                NUMBER_OF_ITERATIONS = nOfIterations;
            }

            public void setPublicKey(string pubKey)
            {
                publicKey = pubKey;
                rsa.FromXmlString(pubKey);
            }

            public string getSimetricKey()
            {
                byte[] dadosEncryptados = rsa.Encrypt(aes.Key, true);
                return Convert.ToBase64String(dadosEncryptados);
            }

            public string getIV()
            {
                byte[] dadosEncryptados = rsa.Encrypt(aes.IV, true);
                return Convert.ToBase64String(dadosEncryptados);
            }

            private string GerarChaveSimetrica(string pass)
            {
                byte[] salt = new byte[] { 0, 1, 0, 8, 1, 9, 9, 7 }; //Adiciona uma dificuldade a password
                Rfc2898DeriveBytes pwdGen = new Rfc2898DeriveBytes(pass, salt, 1000); //Gera a password

                //Gerar a KEY
                byte[] key = pwdGen.GetBytes(16); //Gera uma password com 16 bytes
                string passB64 = Convert.ToBase64String(key);
                return passB64;
            }

            private string GerarIV(string pass)
            {
                byte[] salt = new byte[] { 6, 3, 7, 8, 0, 1, 2, 3 }; //Adiciona uma dificuldade a IV
                Rfc2898DeriveBytes pwdGen = new Rfc2898DeriveBytes(pass, salt, 1000); //Gera a IV

                //Gerar a IV
                byte[] iv = pwdGen.GetBytes(16); //Gera uma IV com 16 bytes
                string ivB64 = Convert.ToBase64String(iv);
                return ivB64;
            }

            public string CifrarTexto(string txt)
            {
                //Variável para guardar o texto decifrado em bytes
                byte[] txtSemCifra = Encoding.UTF8.GetBytes(txt);
                //Variável para guardar o texto cifrado em bytes
                byte[] txtCifrado;

                //Reserva espaço em memória para por lá o texto e cifrá-lo
                MemoryStream ms = new MemoryStream();
                //Inicializar o sistema de cifragem (Write)
                CryptoStream cs = new CryptoStream(ms, this.aes.CreateEncryptor(), CryptoStreamMode.Write);

                //Cifra os dados
                cs.Write(txtSemCifra, 0, txtSemCifra.Length);
                cs.Close();

                //Guarda os dados cifrados que estão em memória
                txtCifrado = ms.ToArray();

                //Converte os bytes para Base64
                string txtCifrado64 = Convert.ToBase64String(txtCifrado);
                return txtCifrado64;
            }
            public string DecifrarTexto(string txt)
            {
                if(txt == "0")
                {
                    return "";
                }
                //Variável para guardar o texto cifrado em bytes
                byte[] txtCifrado = Convert.FromBase64String(txt);

                //Reserva espaço em memória para por lá o texto e decifrá-lo
                MemoryStream ms = new MemoryStream(txtCifrado);
                //Inicializa o sistema de decifragem (Read)
                CryptoStream cs = new CryptoStream(ms, this.aes.CreateDecryptor(), CryptoStreamMode.Read);

                //Variável para guardar o texto decifrado em bytes
                byte[] txtDecifrado = new byte[ms.Length];
                //Variável para ter o número de bytes decifrados
                int bytesLidos = 0;

                //Decifra os dados
                bytesLidos = cs.Read(txtDecifrado, 0, txtDecifrado.Length);
                cs.Close();

                //Converte os bytes para (texto)
                string txtDecifrado64 = Encoding.UTF8.GetString(txtDecifrado, 0, bytesLidos);
                return txtDecifrado64;
            }

            public bool verifySignData(string data, string dataSigned) //Verifica os dados utilizando assinaturas digitais
            {
                byte[] dataToHash = Encoding.UTF8.GetBytes(data);
                byte[] hash;
                using (SHA1 sha1 = SHA1.Create())
                {
                    hash = sha1.ComputeHash(dataToHash);
                }
                byte[] signature = Convert.FromBase64String(dataSigned);
                if (rsa.VerifyHash(hash, CryptoConfig.MapNameToOID("SHA1"), signature))
                {
                    return true;
                }
                return false;

            }

            private static byte[] GenerateSalt()
            {
                //Generate a cryptographic random number.
                RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
                byte[] buff = new byte[SALTSIZE];
                rng.GetBytes(buff);
                return buff;
            }

            private static byte[] GenerateSaltedHash(string plainText, byte[] salt)
            {
                Rfc2898DeriveBytes rfc2898 = new Rfc2898DeriveBytes(plainText, salt, NUMBER_OF_ITERATIONS);
                return rfc2898.GetBytes(32);
            }

            public bool VerifyLogin(string username, string password)
            {
                SqlConnection conn = null;
                try
                {
                    // Configurar ligação à Base de Dados
                    conn = new SqlConnection();
                    conn.ConnectionString = String.Format(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename='C:\Users\yuril\Documents\Disciplinas\TS\TP\Cliente\Servidor\Database-Users.mdf';Integrated Security=True");

                    // Abrir ligação à Base de Dados
                    conn.Open();

                    // Declaração do comando SQL
                    String sql = "SELECT * FROM Users WHERE Username = @username";
                    SqlCommand cmd = new SqlCommand();
                    cmd.CommandText = sql;

                    // Declaração dos parâmetros do comando SQL
                    SqlParameter param = new SqlParameter("@username", username);

                    // Introduzir valor ao parâmentro registado no comando SQL
                    cmd.Parameters.Add(param);

                    // Associar ligação à Base de Dados ao comando a ser executado
                    cmd.Connection = conn;

                    // Executar comando SQL
                    SqlDataReader reader = cmd.ExecuteReader();

                    if (!reader.HasRows)
                    {
                        Register(username, password);
                        Console.WriteLine("Usuário criado com sucesso!");
                        return true;
                    }

                    // Ler resultado da pesquisa
                    reader.Read();

                    // Obter Hash (password + salt)
                    byte[] saltedPasswordHashStored = (byte[])reader["SaltedPasswordHash"];

                    // Obter salt
                    byte[] saltStored = (byte[])reader["Salt"];

                    conn.Close();

                    byte[] hash = GenerateSaltedHash(password, saltStored);

                    return saltedPasswordHashStored.SequenceEqual(hash);
                }
                catch
                {
                    Console.WriteLine("Ocorreu um erro");
                    return false;
                }
            }

            public void Register(string username, string senha)
            {
                byte[] saltedPasswordHash, salt;
                salt = GenerateSalt();
                saltedPasswordHash = GenerateSaltedHash(senha, salt);
                SqlConnection conn = null;
                try
                {

                    // Configurar ligação à Base de Dados
                    conn = new SqlConnection();
                    conn.ConnectionString = String.Format(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename='C:\Users\yuril\Documents\Disciplinas\TS\TP\Cliente\Servidor\Database-Users.mdf';Integrated Security=True");

                    // Abrir ligação à Base de Dados
                    conn.Open();

                    // Declaração dos parâmetros do comando SQL
                    SqlParameter paramUsername = new SqlParameter("@username", username);
                    SqlParameter paramPassHash = new SqlParameter("@saltedPasswordHash", saltedPasswordHash);
                    SqlParameter paramSalt = new SqlParameter("@salt", salt);

                    // Declaração do comando SQL
                    String sql = "INSERT INTO Users (Username, SaltedPasswordHash, Salt) VALUES (@username,@saltedPasswordHash,@salt)";

                    // Prepara comando SQL para ser executado na Base de Dados
                    SqlCommand cmd = new SqlCommand(sql, conn);

                    // Introduzir valores aos parâmentros registados no comando SQL
                    cmd.Parameters.Add(paramUsername);
                    cmd.Parameters.Add(paramPassHash);
                    cmd.Parameters.Add(paramSalt);

                    // Executar comando SQL
                    int lines = cmd.ExecuteNonQuery();

                    // Fechar ligação
                    conn.Close();
                    if (lines == 0)
                    {
                        // Se forem devolvidas 0 linhas alteradas então o não foi executado com sucesso
                        throw new Exception("Erro enquanto inseria um usuário");
                    }
                    Console.WriteLine("Registado com sucesso!");
                }
                catch (Exception e)
                {
                    throw new Exception("Erro enquanto inseria um usuário:" + e.Message);
                }
            }
        }
    }
}
