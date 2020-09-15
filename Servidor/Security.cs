using System;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using System.Data.SqlClient;

namespace Security
{
    public class security //Classe que trata de toda a segurança da conexão
    {
        private AesCryptoServiceProvider aes;
        private RSACryptoServiceProvider rsa;
        private static int SALTSIZE;
        private static int NUMBER_OF_ITERATIONS;

        public security(int saltSize = 8, int nOfIterations = 50000)
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
            if (txt == "0")
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
