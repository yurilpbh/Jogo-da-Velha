using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Net;
using EI.SI;
using System.IO;
using Security;
using System.Linq;

namespace JogoGalo
{
    public partial class Form1 : Form
    {
        private const int PORT = 10000;
        TcpClient tcpClient;
        NetworkStream networkStream;
        ProtocolSI protocolSI;
        string lastMsg;
        securityData protocolSecurity;
        List<Button> buttons;
        Dictionary<string, int> listaJogadores = new Dictionary<string, int>();
        public Form1()
        {
            InitializeComponent();
            this.Width = 670;
            //Desabilita os botões que não possuem funcionalidade de imediato
            btConectar.Enabled = false;
            btMensagem.Enabled = false;
            btSair.Enabled = false;
            tbMensagem.Enabled = false;
            tbSala.Enabled = false;
            btTrocarPosicao.Enabled = false;
            btVarJogadores.Enabled = false;
            tbProxJogador.Enabled = false;
            buttons = new List<Button>();
            buttons.Add(bt00); buttons.Add(bt01); buttons.Add(bt02);
            buttons.Add(bt10); buttons.Add(bt11); buttons.Add(bt12);
            buttons.Add(bt20); buttons.Add(bt21); buttons.Add(bt22);
            foreach(Button button in buttons) //Desabilita todos os botões de jogadas
            {
                button.Enabled = false;
            }
            //Inicializa o background que é responsável por ficar a escuta do servidor
            BackgroundWorker backgroundWorker1 = new BackgroundWorker();
            backgroundWorker1.DoWork += new DoWorkEventHandler(backgroundWorker1_DoWork);
            backgroundWorker1.RunWorkerCompleted += new RunWorkerCompletedEventHandler(backgroundWorker1_RunWorkerCompleted);

            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, PORT);
            tcpClient = new TcpClient();
            tcpClient.Connect(endPoint);
            //Obtem um fluxo para leitura e escrita.
            networkStream = tcpClient.GetStream();
            //Preparação da comunidade utilizando a classe desenvolvida pelo SI
            protocolSI = new ProtocolSI();

            //Cria a classe segura que contém as chaves públicas e privada
            protocolSecurity = new securityData();

            //Prepara o envio da chave pública
            byte[] pctPublicKey = protocolSI.Make(ProtocolSICmdType.PUBLIC_KEY, protocolSecurity.getChavePublica());

            //Envia a chave pública
            networkStream.Write(pctPublicKey, 0, pctPublicKey.Length);
            esperaACK();

            //Espera a chave simétrica
            networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
            while (protocolSI.GetCmdType() != ProtocolSICmdType.SYM_CIPHER_DATA)
            {
                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
            }
            enviaACK();
            protocolSecurity.setChaveSimetrica(protocolSI.GetStringFromData());

            //Espera o IV
            networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
            while (protocolSI.GetCmdType() != ProtocolSICmdType.IV)
            {
                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
            }
            enviaACK();
            protocolSecurity.setIV(protocolSI.GetStringFromData());

        }

        private void tbLogin_Click(object sender, EventArgs e)
        {
            //Prepara o envio dos dados de login
            string login = tbUsuario.Text;
            string senha = tbSenha.Text;
            byte[] pctLogin = protocolSI.Make(ProtocolSICmdType.USER_OPTION_1, protocolSecurity.cifrarMensagem(login));
            byte[] pctSenha = protocolSI.Make(ProtocolSICmdType.SECRET_KEY, protocolSecurity.cifrarMensagem(senha));

            //Envia o login
            networkStream.Write(pctLogin, 0, pctLogin.Length);
            esperaACK();

            //Envia a senha
            networkStream.Write(pctSenha, 0, pctSenha.Length);
            networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
            while (protocolSI.GetCmdType() != ProtocolSICmdType.ACK)
            {
                if(protocolSI.GetCmdType() == ProtocolSICmdType.USER_OPTION_3)
                {
                    MessageBox.Show("Senha incorreta!");
                    enviaACK();
                    return;
                }
                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
            }
            lastMsg = protocolSecurity.decifrarMensagem(protocolSI.GetStringFromData());
            string[] msg = lastMsg.Split('/'); ;
            tbJogadores.Text = msg[0];
            tbPontos.Text = msg[1];

            btConectar.Enabled = true;
            tbSala.Enabled = true;
            btSair.Enabled = true;

            tbUsuario.Enabled = false;
            tbSenha.Enabled = false;
            btEntrar.Enabled = false;
            enviaACK();
        }

        private void btSair_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void CloseClient()
        {
            this.backgroundWorker1.CancelAsync();
            //Envio da mensagem para delisgar a conexão
            byte[] eot = protocolSI.Make(ProtocolSICmdType.EOT);
            if (networkStream.CanWrite)
            {
                networkStream.Write(eot, 0, eot.Length);
            }
            System.Threading.Thread.Sleep(100);
            networkStream.Close();
            tcpClient.Close();
        }

        private void Form1_FormClosing(object sender, EventArgs e)
        {
            CloseClient();
        }

        private void btConectar_Click(object sender, EventArgs e)
        {
            //Envia a sala para qual deseja conectar-se
            string sala = tbSala.Text;
            byte[] pctSala = protocolSI.Make(ProtocolSICmdType.USER_OPTION_2, protocolSecurity.cifrarMensagem(sala));
            networkStream.Write(pctSala, 0, pctSala.Length);
            networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
            while (protocolSI.GetCmdType() != ProtocolSICmdType.ACK)
            {
                if (protocolSI.GetCmdType() == ProtocolSICmdType.USER_OPTION_3)
                {
                    MessageBox.Show("Sala cheia!");
                    enviaACK();
                    return;
                }
                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
            }
            tbMensagem.Enabled = true;
            btMensagem.Enabled = true;
            btTrocarPosicao.Enabled = true;
            btVarJogadores.Enabled = true;
            btConectar.Enabled = false;
            enviaACK();
            backgroundWorker1.RunWorkerAsync();
        }

        private void btMensagem_Click(object sender, EventArgs e)
        {
            //Envia a mensagem para o chat (antes ela passa pelo servidor)
            string chat = tbMensagem.Text;
            byte[] pctChat = protocolSI.Make(ProtocolSICmdType.DATA, protocolSecurity.cifrarMensagem(chat));
            networkStream.Write(pctChat, 0, pctChat.Length);
        }

        private void enviaACK()
        { 
            //Envia o ACK para o servidor
            byte[] ack = protocolSI.Make(ProtocolSICmdType.ACK);
            networkStream.Write(ack, 0, ack.Length);
        }

        private void esperaACK(int timeout = -1)
        {
            networkStream.ReadTimeout = timeout;
            
            networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
            while (protocolSI.GetCmdType() != ProtocolSICmdType.ACK)
            {
                try
                {
                    networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                }
                catch (IOException)
                {
                    return;
                }
            }
        }

        void atualizaClient(BackgroundWorker worker, DoWorkEventArgs e)
        {
            //Função que fica a escutar o servidor de forma assíncrona esperando novas mensagens
            if (networkStream.CanRead)
            {
                try
                {
                    networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                }
                catch (IOException)
                {
                    Console.WriteLine("IOException");
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("InvalidOperationException");
                }
            }
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            atualizaClient(worker, e);
        }

        private Dictionary<string, int> atualizaListaDeJogadores(string lista)
        {
            var listaJogadores = new Dictionary<string, int>();
            string[] jogadores = lista.Split(';');
            tbJogadores.Text = "";
            tbPontos.Text = "";
            foreach (string campo in jogadores) // Atualiza a lista de jogadores na sala
            {
                if(campo != "")
                {
                    tbJogadores.Text = tbJogadores.Text + campo.Split(',')[0] + Environment.NewLine;
                    tbPontos.Text = tbPontos.Text + campo.Split(',')[1] + Environment.NewLine;
                    listaJogadores.Add(campo.Split(',')[0], Convert.ToInt32(campo.Split(',')[1]));
                }
            }
            return listaJogadores;
        }

        private void atualizaListaDeJogadores(Dictionary<string, int> lista, string jogador, int points)
        {
            lista[jogador] = points;
            var items = from pair in lista
                        orderby pair.Value descending
                        select pair;
            tbJogadores.Text = "";
            tbPontos.Text = "";
            foreach (KeyValuePair<string, int> pair in items)
            {
                tbJogadores.Text = tbJogadores.Text + pair.Key + Environment.NewLine;
                tbPontos.Text = tbPontos.Text + pair.Value.ToString() + Environment.NewLine;
            }
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (protocolSI.GetCmdType() != ProtocolSICmdType.NACK)
            {
                lastMsg = protocolSecurity.decifrarMensagem(protocolSI.GetStringFromData());
                string[] msg;
                switch (protocolSI.GetCmdType())
                {
                    case ProtocolSICmdType.USER_OPTION_1: //Atualiza nome dos jogadores
                        msg = lastMsg.Split('/');
                        if (msg[0]=="1")
                        {
                            tbJogador1.Text = msg[1]; //Atualiza nome do jogador 1
                        }
                        else if (msg[0]=="2")
                        {
                            tbJogador2.Text = msg[1]; //Atualiza nome do jogador 2
                            listaJogadores = atualizaListaDeJogadores(msg[2]);
                        }
                        else
                        {
                            tbJogador1.Text = msg[1]; //Atualiza nome do jogador 1
                            tbJogador2.Text = msg[2]; //Atualiza nome do jogador 2
                            tbPontos1.Text = msg[3]; //Atualiza pontos do jogador 1
                            tbPontos2.Text = msg[4]; //Atualiza pontos do jogador 2
                            tbEmpates.Text = msg[5]; //Atualiza pontos de empate
                            tbProxJogador.Text = msg[6] + Environment.NewLine; //Coloca o jogador na lista de espera
                            listaJogadores = atualizaListaDeJogadores(msg[7]);
                        }
                        break;

                    case ProtocolSICmdType.USER_OPTION_2: //Ocorre quando um jogador sai da sala
                        msg = lastMsg.Split('/');
                        MessageBox.Show(String.Format("{0} deixou a sala", msg[0]));
                        if (msg[1].Equals("1")) //Se o jogador que deixou a sala for o primeiro, o atual passa a esse lugar
                        {
                            tbJogador1.Text = tbJogador2.Text;
                            tbPontos1.Text = tbPontos2.Text;
                        }
                        tbJogador2.Text = "";
                        tbPontos2.Text = "";
                        foreach (Button button in buttons)
                        {
                            button.Text = "";
                            button.Enabled = false;
                        }
                        break;

                    case ProtocolSICmdType.DATA: //Atualiza o chat
                        tbChat.AppendText(lastMsg + Environment.NewLine);
                        break;

                    case ProtocolSICmdType.USER_OPTION_3: //Habilita os botões de jogo
                        foreach (Button button in buttons)
                        {
                            button.Enabled = true;
                        }
                        //Mantém os pontos do jogador que está na sala
                        tbPontos1.Text = tbPontos1.Text == "" ? "0" : tbPontos1.Text;
                        tbPontos2.Text = "0";
                        tbEmpates.Text = tbEmpates.Text == "" ? "0" : tbEmpates.Text;
                        break;

                    case ProtocolSICmdType.USER_OPTION_4: //Jogadas ou movimento inválidos
                        MessageBox.Show(lastMsg);
                        break;

                    case ProtocolSICmdType.USER_OPTION_5: //Atualiza a jogada feita
                        string[] coordenadas = lastMsg.Split('/');
                        jogada(coordenadas[0], coordenadas[1]);
                        break;

                    case ProtocolSICmdType.USER_OPTION_6: //Diz quem ganhou e reinicia o jogo
                        msg = lastMsg.Split('/');
                        MessageBox.Show(msg[0] + " " + msg[1]);
                        int pontos = 0;
                        if (msg[0] == tbJogador1.Text) //Jogador 1 ganhou
                        {
                            pontos = int.Parse(tbPontos1.Text) + 1;
                            tbPontos1.Text = pontos.ToString();
                            atualizaListaDeJogadores(listaJogadores, tbJogador1.Text, Convert.ToInt32(tbPontos1.Text) + listaJogadores[tbJogador1.Text]);
                        }
                        else if (msg[0] == tbJogador2.Text) //Jogador 2 ganhou
                        {
                            pontos = int.Parse(tbPontos2.Text) + 1;
                            tbPontos2.Text = pontos.ToString();
                            atualizaListaDeJogadores(listaJogadores, tbJogador2.Text, Convert.ToInt32(tbPontos2.Text) + listaJogadores[tbJogador2.Text]);
                        }
                        else if (msg[0] == "") //O jogo deu empate e é reiniciado
                        {
                            pontos = int.Parse(tbEmpates.Text) + 1;
                            tbEmpates.Text = pontos.ToString();
                        }
                        else
                        {
                            MessageBox.Show("Algo errado aconteceu");
                        }
                        foreach (Button button in buttons)
                        {
                            button.Text = "";
                            button.Enabled = true;
                        }
                        break;

                    case ProtocolSICmdType.USER_OPTION_7: //Troca de posição
                        msg = lastMsg.Split('/');
                        if(msg.Length == 1) //Usuário solicitou a troca
                        {
                            DialogResult result = MessageBox.Show(msg[0],"Solicitação de troca", MessageBoxButtons.YesNo);
                            switch (result)
                            {
                                case DialogResult.Yes:
                                    byte[] aceito = protocolSI.Make(ProtocolSICmdType.USER_OPTION_1);
                                    networkStream.Write(aceito, 0, aceito.Length);
                                    string aux = tbJogador1.Text;
                                    tbJogador1.Text = tbJogador2.Text;
                                    tbJogador2.Text = aux;
                                    aux = tbPontos1.Text;
                                    tbPontos1.Text = tbPontos2.Text;
                                    tbPontos2.Text = aux;
                                    break;
                                case DialogResult.No:
                                    byte[] recuso = protocolSI.Make(ProtocolSICmdType.USER_OPTION_2);
                                    networkStream.Write(recuso, 0, recuso.Length);
                                    break;
                                default:
                                    break;
                            }
                        } else if (msg[0] == "0") //Solicitação negada
                        {
                            MessageBox.Show(msg[1]);
                            btTrocarPosicao.Enabled = true;
                        } else //Solicitação aceita
                        {
                            MessageBox.Show(msg[1]);
                            string aux = tbJogador1.Text;
                            tbJogador1.Text = tbJogador2.Text;
                            tbJogador2.Text = aux;
                            aux = tbPontos1.Text;
                            tbPontos1.Text = tbPontos2.Text;
                            tbPontos2.Text = aux;
                            btTrocarPosicao.Enabled = true;
                        }
                        break;

                    case ProtocolSICmdType.USER_OPTION_8:
                        btVarJogadores.Enabled = false;
                        MessageBox.Show(lastMsg);
                        break;

                    case ProtocolSICmdType.USER_OPTION_9:
                        msg = lastMsg.Split('/');
                        if(tbJogador1.Text == msg[1])
                        {
                            tbJogador1.Text = "";
                        } else
                        {
                            tbJogador2.Text = "";
                        }
                        tbPontos1.Text = "";
                        tbPontos2.Text = "";
                        break;

                    case ProtocolSICmdType.ACK:
                        break;

                    default:
                        break;
                }
                if (networkStream.CanWrite) { enviaACK(); }
            }
            else if (lastMsg != protocolSI.GetStringFromData())
            {
                byte[] msgLost = protocolSI.Make(ProtocolSICmdType.NACK);
                networkStream.Write(msgLost, 0, msgLost.Length);
            }
            else
            {
                if (networkStream.CanWrite) { enviaACK(); }
            }
            backgroundWorker1.RunWorkerAsync();
        }

        private void jogada(string button, string symbol) //Identifica a jogada feita
        {
            switch (button)
            {
                case "00":
                    bt00.Text = symbol;
                    bt00.Enabled = false;
                    break;

                case "01":
                    bt01.Text = symbol;
                    bt01.Enabled = false;
                    break;

                case "02":
                    bt02.Text = symbol;
                    bt02.Enabled = false;
                    break;

                case "10":
                    bt10.Text = symbol;
                    bt10.Enabled = false;
                    break;

                case "11":
                    bt11.Text = symbol;
                    bt11.Enabled = false;
                    break;

                case "12":
                    bt12.Text = symbol;
                    bt12.Enabled = false;
                    break;

                case "20":
                    bt20.Text = symbol;
                    bt20.Enabled = false;
                    break;

                case "21":
                    bt21.Text = symbol;
                    bt21.Enabled = false;
                    break;

                case "22":
                    bt22.Text = symbol;
                    bt22.Enabled = false;
                    break;
            }
        }

        private void bt00_Click(object sender, EventArgs e)
        {
            string msg = "0/0";
            byte[] pctChat = protocolSI.Make(ProtocolSICmdType.USER_OPTION_3, protocolSecurity.cifrarMensagem(msg));
            networkStream.Write(pctChat, 0, pctChat.Length);
            byte[] pctChatSigned = protocolSI.Make(ProtocolSICmdType.USER_OPTION_4, protocolSecurity.cifrarMensagem(protocolSecurity.signHash(msg)));
            networkStream.Write(pctChatSigned, 0, pctChatSigned.Length);
        }

        private void bt01_Click(object sender, EventArgs e)
        {
            string msg = "0/1";
            byte[] pctChat = protocolSI.Make(ProtocolSICmdType.USER_OPTION_3, protocolSecurity.cifrarMensagem(msg));
            networkStream.Write(pctChat, 0, pctChat.Length);

            byte[] pctChatSigned = protocolSI.Make(ProtocolSICmdType.USER_OPTION_4, protocolSecurity.cifrarMensagem(protocolSecurity.signHash(msg)));
            networkStream.Write(pctChatSigned, 0, pctChatSigned.Length);
        }

        private void bt02_Click(object sender, EventArgs e)
        {
            string msg = "0/2";
            byte[] pctChat = protocolSI.Make(ProtocolSICmdType.USER_OPTION_3, protocolSecurity.cifrarMensagem(msg));
            networkStream.Write(pctChat, 0, pctChat.Length);

            byte[] pctChatSigned = protocolSI.Make(ProtocolSICmdType.USER_OPTION_4, protocolSecurity.cifrarMensagem(protocolSecurity.signHash(msg)));
            networkStream.Write(pctChatSigned, 0, pctChatSigned.Length);
        }

        private void bt10_Click(object sender, EventArgs e)
        {
            string msg = "1/0";
            byte[] pctChat = protocolSI.Make(ProtocolSICmdType.USER_OPTION_3, protocolSecurity.cifrarMensagem(msg));
            networkStream.Write(pctChat, 0, pctChat.Length);

            byte[] pctChatSigned = protocolSI.Make(ProtocolSICmdType.USER_OPTION_4, protocolSecurity.cifrarMensagem(protocolSecurity.signHash(msg)));
            networkStream.Write(pctChatSigned, 0, pctChatSigned.Length);
        }

        private void bt11_Click(object sender, EventArgs e)
        {
            string msg = "1/1";
            byte[] pctChat = protocolSI.Make(ProtocolSICmdType.USER_OPTION_3, protocolSecurity.cifrarMensagem(msg));
            networkStream.Write(pctChat, 0, pctChat.Length);

            byte[] pctChatSigned = protocolSI.Make(ProtocolSICmdType.USER_OPTION_4, protocolSecurity.cifrarMensagem(protocolSecurity.signHash(msg)));
            networkStream.Write(pctChatSigned, 0, pctChatSigned.Length);
        }

        private void bt12_Click(object sender, EventArgs e)
        {
            string msg = "1/2";
            byte[] pctChat = protocolSI.Make(ProtocolSICmdType.USER_OPTION_3, protocolSecurity.cifrarMensagem(msg));
            networkStream.Write(pctChat, 0, pctChat.Length);

            byte[] pctChatSigned = protocolSI.Make(ProtocolSICmdType.USER_OPTION_4, protocolSecurity.cifrarMensagem(protocolSecurity.signHash(msg)));
            networkStream.Write(pctChatSigned, 0, pctChatSigned.Length);
        }

        private void bt20_Click(object sender, EventArgs e)
        {
            string msg = "2/0";
            byte[] pctChat = protocolSI.Make(ProtocolSICmdType.USER_OPTION_3, protocolSecurity.cifrarMensagem(msg));
            networkStream.Write(pctChat, 0, pctChat.Length);

            byte[] pctChatSigned = protocolSI.Make(ProtocolSICmdType.USER_OPTION_4, protocolSecurity.cifrarMensagem(protocolSecurity.signHash(msg)));
            networkStream.Write(pctChatSigned, 0, pctChatSigned.Length);
        }

        private void bt21_Click(object sender, EventArgs e)
        {
            string msg = "2/1";
            byte[] pctChat = protocolSI.Make(ProtocolSICmdType.USER_OPTION_3, protocolSecurity.cifrarMensagem(msg));
            networkStream.Write(pctChat, 0, pctChat.Length);

            byte[] pctChatSigned = protocolSI.Make(ProtocolSICmdType.USER_OPTION_4, protocolSecurity.cifrarMensagem(protocolSecurity.signHash(msg)));
            networkStream.Write(pctChatSigned, 0, pctChatSigned.Length);
        }

        private void bt22_Click(object sender, EventArgs e)
        {
            string msg = "2/2";
            byte[] pctChat = protocolSI.Make(ProtocolSICmdType.USER_OPTION_3, protocolSecurity.cifrarMensagem(msg));
            networkStream.Write(pctChat, 0, pctChat.Length);

            byte[] pctChatSigned = protocolSI.Make(ProtocolSICmdType.USER_OPTION_4, protocolSecurity.cifrarMensagem(protocolSecurity.signHash(msg)));
            networkStream.Write(pctChatSigned, 0, pctChatSigned.Length);
        }

        private void btExpand_Click(object sender, EventArgs e)
        {
            if(btExpand.Text == ">")
            {
                btExpand.Text = "<";
                this.Width = 960;
            }
            else
            {
                btExpand.Text = ">";
                this.Width = 670;
            }
            
        }

        private void btTrocarPosicao_Click(object sender, EventArgs e)
        {
            byte[] trocarPosicao = protocolSI.Make(ProtocolSICmdType.USER_OPTION_5);
            networkStream.Write(trocarPosicao, 0, trocarPosicao.Length);
            btTrocarPosicao.Enabled = false;
        }

        private void btVarJogadores_Click(object sender, EventArgs e)
        {
            byte[] varJogadores = protocolSI.Make(ProtocolSICmdType.USER_OPTION_6);
            networkStream.Write(varJogadores, 0, varJogadores.Length);
            tbProxJogador.Enabled = true;
        }
    }
}
