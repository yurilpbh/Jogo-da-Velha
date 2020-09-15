using System;
using System.Collections.Generic;
using System.Net.Sockets;
using EI.SI;
using System.Threading;
using System.IO;
using Room;
using Security;

namespace ClienteHandler
{
    public class ClientHandler
    {
        private static Mutex connection = new Mutex(); //Mutex utilizado para fazer broadcast para os clientes
        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private ProtocolSI protocolSI;
        private room room;
        private static List<room> rooms = new List<room>();
        private security security;
        private int clientID;
        private string nomeJogador;
        private byte[] simetricKey;
        private byte[] IV;

        public ClientHandler(TcpClient client, int clientID)
        {
            tcpClient = client;
            this.clientID = clientID;
            security = new security();
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
                if (room.getClientList().Count == 1)
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
                        ProtocolSICmdType protocolSICmdType = protocolSI.GetCmdType();
                        while (protocolSICmdType != ProtocolSICmdType.USER_OPTION_1 && protocolSICmdType != ProtocolSICmdType.USER_OPTION_2)
                        {
                            newNetworkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                            protocolSICmdType = protocolSI.GetCmdType();
                            int a = 1;
                        }
                        connection.ReleaseMutex(); //Libera o networkStream
                        Console.WriteLine("oi");
                        if (protocolSICmdType == ProtocolSICmdType.USER_OPTION_1)
                        {
                            Console.WriteLine("ei");
                            msg = "1/Solicitação aceita";
                            msgByte = protocolSI.Make(ProtocolSICmdType.USER_OPTION_7, security.CifrarTexto(msg));
                            networkStream.Write(msgByte, 0, msgByte.Length);
                            esperaACK();
                            room.trocaJogadores();
                        }
                        else
                        {
                            Console.WriteLine("ou");
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

                        foreach (room sala in rooms) //Percorre a lista de salas verificando se a sala na qual o cliente deseja conectar-se já existe
                        {
                            if (sala.getName() == salaDesejada)
                            {
                                if (!sala.isFull()) //Verifica se a sala não está cheia
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
                            this.room = new room(salaDesejada);
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
                        if (security.VerifyLogin(this.nomeJogador, senha))
                        { //Autentica o jogador
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
                        if (room != null && room.getClientList().Count == 2)
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
}
