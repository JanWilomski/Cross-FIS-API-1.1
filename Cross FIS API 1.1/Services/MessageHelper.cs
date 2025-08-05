using System;
using System.Linq;
using System.Text;

namespace Cross_FIS_API_1._1.Services
{
    public static class MessageHelper
    {
        /// <summary>
        /// Konwertuje tablicę bajtów na reprezentację hex string dla celów debugowania
        /// </summary>
        /// <param name="bytes">Tablica bajtów do konwersji</param>
        /// <param name="maxLength">Maksymalna długość do wyświetlenia</param>
        /// <returns>Reprezentacja hex</returns>
        public static string BytesToHexString(byte[] bytes, int maxLength = 50)
        {
            if (bytes == null || bytes.Length == 0)
                return "";

            int length = Math.Min(bytes.Length, maxLength);
            var sb = new StringBuilder();
            
            for (int i = 0; i < length; i++)
            {
                sb.Append(bytes[i].ToString("X2"));
                if (i < length - 1)
                    sb.Append(" ");
            }
            
            if (bytes.Length > maxLength)
                sb.Append("...");
                
            return sb.ToString();
        }

        /// <summary>
        /// Waliduje podstawową strukturę wiadomości FIS
        /// </summary>
        /// <param name="message">Wiadomość do walidacji</param>
        /// <returns>True jeśli struktura jest poprawna</returns>
        public static bool ValidateFisMessage(byte[] message)
        {
            if (message == null || message.Length < 37) // LG(2) + Header(32) + Footer(3)
                return false;

            try
            {
                // Sprawdź długość wiadomości (pierwsze 2 bajty - LG)
                int messageLength = message[0] + message[1] * 256;
                if (messageLength != message.Length)
                    return false;

                // Sprawdź STX w nagłówku (powinien być na pozycji 2 i mieć wartość 2)
                if (message[2] != 2) // STX
                    return false;

                // Sprawdź ETX w stopce (ostatni bajt powinien mieć wartość 3)
                if (message[message.Length - 1] != 3) // ETX
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Parsuje nagłówek wiadomości FIS
        /// </summary>
        /// <param name="message">Wiadomość</param>
        /// <param name="headerOffset">Offset nagłówka (zwykle 2 - po LG)</param>
        /// <returns>Sparsowany nagłówek lub null w przypadku błędu</returns>
        public static FisHeader ParseHeader(byte[] message, int headerOffset)
        {
            if (message == null || message.Length < headerOffset + 32)
                return null;

            try
            {
                var header = new FisHeader();
                int offset = headerOffset;

                // STX (1 bajt)
                header.STX = message[offset++];

                // API Version (1 bajt)
                header.ApiVersion = (char)message[offset++];

                // Request Size (5 bajtów ASCII)
                header.RequestSize = Encoding.ASCII.GetString(message, offset, 5);
                offset += 5;

                // Called Logical Identifier (5 bajtów ASCII)
                header.CalledLogicalId = Encoding.ASCII.GetString(message, offset, 5);
                offset += 5;

                // Filler (5 bajtów)
                offset += 5;

                // Calling Logical Identifier (5 bajtów ASCII)
                header.CallingLogicalId = Encoding.ASCII.GetString(message, offset, 5);
                offset += 5;

                // Filler (2 bajty)
                offset += 2;

                // Request Number (5 bajtów ASCII)
                header.RequestNumber = Encoding.ASCII.GetString(message, offset, 5);
                offset += 5;

                // Pozostałe 3 bajty to filler
                
                return header;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Reprezentuje sparsowany nagłówek wiadomości FIS
    /// </summary>
    public class FisHeader
    {
        public byte STX { get; set; }
        public char ApiVersion { get; set; }
        public string RequestSize { get; set; } = string.Empty;
        public string CalledLogicalId { get; set; } = string.Empty;
        public string CallingLogicalId { get; set; } = string.Empty;
        public string RequestNumber { get; set; } = string.Empty;
    }
}