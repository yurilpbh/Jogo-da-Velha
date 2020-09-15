using System;

namespace Game
{
    public partial class Jogo
    {
        private char[,] matrix;
        private int moves;
        private char ultimoJogador = 'O';
        public Jogo()
        {
            this.matrix = new char[3, 3] { { ' ', ' ', ' ' }, { ' ', ' ', ' ' }, { ' ', ' ', ' ' } };
            this.moves = 0;
        }

        private Boolean isWinner(int playerSymbol) //Verifica se a jogada atual levou o jogador à vitória
        {
            for (int i = 0; i < 3; i++)
            {
                int colCount = 0;
                for (int j = 0; j < 3; j++)
                {
                    if (matrix[i, j] == playerSymbol)
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
            if ((matrix[0, 0] == playerSymbol && matrix[1, 1] == playerSymbol && matrix[2, 2] == playerSymbol) ||
                 (matrix[0, 2] == playerSymbol && matrix[1, 1] == playerSymbol && matrix[2, 0] == playerSymbol))
            {
                return true;
            }
            return false;
        }

        //Faz a jogada executada pelo jogador e verifica se o movimento é válido, se houve empate ou se houve vitória
        public int move(int line, int col, int player)
        {
            char playerSymbol = player == 0 ? 'X' : 'O';
            if (playerSymbol == ultimoJogador)
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
}
