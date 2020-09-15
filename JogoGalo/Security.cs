using System;
using System.Text;
using System.Security.Cryptography;
using System.IO;

namespace Security
{
    public partial class securityData //Cuida da segurança da conexão e dos dados
    {
        private RSACryptoServiceProvider rsa;
        private AesCryptoServiceProvider aes;
        private string publicKey;
        private string senha;

        public securityData()
        {
            rsa = new RSACryptoServiceProvider();
            aes = new AesCryptoServiceProvider();
            publicKey = rsa.ToXmlString(false); //Criar e devolver uma string que contém a chave pública
        }
        public string getChavePublica()
        {
            return publicKey;
        }

        public void setSenha(string senha)
        {
            this.senha = senha;
        }
        public string getSenha()
        {
            return senha;
        }

        public void setChaveSimetrica(string dataEncrypted)
        {
            byte[] dados = Convert.FromBase64String(dataEncrypted);
            //Decifra os dados utilizando RSA
            aes.Key = rsa.Decrypt(dados, true);
        }

        public void setIV(string dataEncrypted)
        {
            byte[] dados = Convert.FromBase64String(dataEncrypted);
            //Decifra os dados utilizando RSA
            aes.IV = rsa.Decrypt(dados, true);
        }

        public string cifrarMensagem(string txt)
        {
            //Variável para guardar o texto decifrado em bytes
            byte[] txtDecifrado = Encoding.UTF8.GetBytes(txt);
            //Variável para guardar o texto cifrado em bytes
            byte[] txtCifrado;

            //Reservar espaço em memória para por lá o texto e cifrá-lo
            MemoryStream ms = new MemoryStream();
            //Inicializar o sistema de cifragem (Write)
            CryptoStream cs = new CryptoStream(ms, this.aes.CreateEncryptor(), CryptoStreamMode.Write);

            //Cifrar os dados
            cs.Write(txtDecifrado, 0, txtDecifrado.Length);
            cs.Close();

            //Guardar os dados cifrados que estão em memória
            txtCifrado = ms.ToArray();
            //Converter os bytes para Base64 (texto)
            string txtCifrado64 = Convert.ToBase64String(txtCifrado);
            return txtCifrado64;
        }

        public string decifrarMensagem(string txt)
        {
            if (txt == "0")
            {
                return "";
            }
            //Variável para guardar o texto cifrado em bytes
            byte[] txtCifrado = Convert.FromBase64String(txt);

            //Reservar espaço em memória para por lá o texto e decifrá-lo
            MemoryStream ms = new MemoryStream(txtCifrado);
            //Inicializar o sistema de decifragem (Read)
            CryptoStream cs = new CryptoStream(ms, this.aes.CreateDecryptor(), CryptoStreamMode.Read);

            //Variável para guardar o texto decifrado em bytes
            byte[] txtDecifrado = new byte[ms.Length];
            //Variável para ter o número de bytes decifrados
            int bytesLidos = 0;

            //Decifrar os dados
            bytesLidos = cs.Read(txtDecifrado, 0, txtDecifrado.Length);
            cs.Close();

            //Converter os bytes para (texto)
            string txtDecifrado64 = Encoding.UTF8.GetString(txtDecifrado, 0, bytesLidos);
            return txtDecifrado64;
        }

        private byte[] hash(string msg)
        {
            byte[] hash;
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] dados = Encoding.UTF8.GetBytes(msg);
                hash = sha1.ComputeHash(dados);
            }
            return hash;
        }
        public string signHash(string msg)
        {
            byte[] signature = rsa.SignHash(hash(msg), CryptoConfig.MapNameToOID("SHA1"));
            return Convert.ToBase64String(signature);
        }
    }
}
