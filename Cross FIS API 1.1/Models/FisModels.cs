using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cross_FIS_API_1._1.Models
{
    public class FisConnectionParameters
    {
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; }
        public string UserNumber { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string CalledLogicalId { get; set; } = string.Empty;
        public string CallingLogicalId { get; set; } = string.Empty;
    }

    public class OrderRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public string Side { get; set; } = "0"; // 0=Buy, 1=Sell
        public string Quantity { get; set; } = string.Empty;
        public string OrderType { get; set; } = "L"; // L=Limit, M=Market, etc.
        public string Price { get; set; } = string.Empty;
        public string Validity { get; set; } = "J"; // J=Day, R=GTC, etc.
        public string ClientReference { get; set; } = string.Empty;
        public string InternalReference { get; set; } = string.Empty;
    }

    public class FisMessage
    {
        private readonly Dictionary<object, string> _fields = new();
        
        public int RequestNumber { get; set; }
        public string CalledLogicalId { get; set; } = string.Empty;
        public string CallingLogicalId { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = " "; // Space for SLE V4

        public void AddField(object fieldId, string value)
        {
            _fields[fieldId] = value;
        }

        public byte[] ToByteArray()
        {
            var dataBuilder = new StringBuilder();
            
            // Build data section
            foreach (var field in _fields)
            {
                if (field.Key is int intKey && intKey == -1)
                {
                    // Special case for fillers
                    dataBuilder.Append(field.Value);
                }
                else if (field.Key is char charKey)
                {
                    // Header fields (B, C, D, G)
                    dataBuilder.Append(field.Value);
                }
                else
                {
                    // Bitmap fields - encode with GL format
                    string encodedValue = EncodeGlField(field.Value);
                    dataBuilder.Append(encodedValue);
                }
            }

            string dataSection = dataBuilder.ToString();
            int dataLength = dataSection.Length;
            
            // Create header (32 bytes)
            var header = new byte[32];
            header[0] = 2; // STX
            Encoding.ASCII.GetBytes(ApiVersion).CopyTo(header, 1); // API version
            Encoding.ASCII.GetBytes(RequestNumber.ToString().PadLeft(5, '0')).CopyTo(header, 2); // Request size (will be updated)
            Encoding.ASCII.GetBytes(CalledLogicalId.PadLeft(5, '0')).CopyTo(header, 7); // Called logical identifier
            Encoding.ASCII.GetBytes("     ").CopyTo(header, 12); // 5 byte filler
            Encoding.ASCII.GetBytes(CallingLogicalId.PadLeft(5, '0')).CopyTo(header, 17); // Calling logical identifier
            Encoding.ASCII.GetBytes("  ").CopyTo(header, 22); // 2 byte filler
            Encoding.ASCII.GetBytes(RequestNumber.ToString().PadLeft(5, '0')).CopyTo(header, 24); // Request number
            Encoding.ASCII.GetBytes("   ").CopyTo(header, 29); // 3 byte filler

            // Update request size in header
            int totalRequestSize = 32 + dataLength + 3; // Header + Data + Footer
            Encoding.ASCII.GetBytes(totalRequestSize.ToString().PadLeft(5, '0')).CopyTo(header, 2);

            // Create footer (3 bytes)
            var footer = new byte[3];
            footer[0] = 32; // Space
            footer[1] = 32; // Space  
            footer[2] = 3;  // ETX

            // Calculate total message length
            int totalLength = 2 + 32 + dataLength + 3; // LG + Header + Data + Footer
            
            // Create complete message
            var message = new byte[totalLength];
            int offset = 0;
            
            // LG (2 bytes)
            message[offset++] = (byte)(totalLength % 256);
            message[offset++] = (byte)(totalLength / 256);
            
            // Header (32 bytes)
            Array.Copy(header, 0, message, offset, header.Length);
            offset += header.Length;
            
            // Data
            if (dataLength > 0)
            {
                byte[] dataBytes = Encoding.ASCII.GetBytes(dataSection);
                Array.Copy(dataBytes, 0, message, offset, dataBytes.Length);
                offset += dataBytes.Length;
            }
            
            // Footer (3 bytes)
            Array.Copy(footer, 0, message, offset, footer.Length);

            return message;
        }

        private string EncodeGlField(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
                
            // GL format: first byte = length + 32, followed by the value
            int length = value.Length;
            char lengthByte = (char)(length + 32);
            return lengthByte + value;
        }
    }
}

namespace Cross_FIS_API_1._1.Services
{
    public class FisMessageEventArgs : EventArgs
    {
        public string MessageType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class ConnectionStatusEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
    }

    public class LogMessageEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
    }
}