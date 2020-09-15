using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Game;
using ClienteHandler;

namespace Room
{
    //Classe que trata dos elementos da sala do jogo
    public partial class room
    {
        private string name, jogador1, jogador2;
        private int pontosJog1, pontosJog2, pontosEmp;
        private List<string> proximoJogador = new List<string>();
        private List<ClientHandler> clientsList = new List<ClientHandler>();
        public Jogo jogo;
        private Boolean allowMultiplePlayers = false;

        public room(string name)
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
            if (jogador1 == null)
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
                return proximoJogador.ElementAt(3 - pos);
            }
        }

        public int getPosJogador(string nome)
        {
            if (nome == jogador1)
            {
                return 1;
            }
            else if (nome == jogador2)
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
                if (clientsList.Count() == 2)
                {
                    return true;
                }
            }
            else if (clientsList.Count() == 10)
            {
                return true;
            }
            return false;
        }

        public void multiplePlayers()
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
}
