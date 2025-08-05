using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cross_FIS_API_1._1.Models;

namespace Cross_FIS_API_1._1.Services
{
    public class FisApiService
    {
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isConnected;
        private FisConnectionParameters _connectionParams;

        public event EventHandler<FisMessageEventArgs> MessageReceived;
        public event EventHandler<ConnectionStatusEventArgs> ConnectionStatusChanged;
        public event EventHandler<LogMessageEventArgs> LogMessage;

        public bool IsConnected => _isConnected && _tcpClient?.Connected == true;

        public async Task ConnectAsync(FisConnectionParameters connectionParams)
        {
            try
            {
                _connectionParams = connectionParams;
                _cancellationTokenSource = new CancellationTokenSource();

                OnLogMessage("Initializing TCP connection...");
                
                // Create TCP connection
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(connectionParams.IpAddress, connectionParams.Port);
                _networkStream = _tcpClient.GetStream();

                OnLogMessage($"Connected to {connectionParams.IpAddress}:{connectionParams.Port}");

                // Send client identification (16 bytes)
                await SendClientIdentificationAsync();

                // Send logical connection request (1100)
                await SendLogicalConnectionRequestAsync();

                // Start message receiving loop
                _ = Task.Run(async () => await MessageReceivingLoop(_cancellationTokenSource.Token));

                _isConnected = true;
                OnConnectionStatusChanged(true, "Connected to FIS server");
            }
            catch (Exception ex)
            {
                OnLogMessage($"Connection failed: {ex.Message}");
                await DisconnectAsync();
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                _isConnected = false;
                
                if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                {
                    // Send disconnection request (1102)
                    if (_networkStream != null && _tcpClient?.Connected == true)
                    {
                        await SendLogicalDisconnectionRequestAsync();
                    }
                    
                    _cancellationTokenSource.Cancel();
                }

                _networkStream?.Close();
                _tcpClient?.Close();

                OnConnectionStatusChanged(false, "Disconnected from FIS server");
                OnLogMessage("Disconnected from server");
            }
            catch (Exception ex)
            {
                OnLogMessage($"Disconnection error: {ex.Message}");
            }
            finally
            {
                _networkStream?.Dispose();
                _tcpClient?.Dispose();
                _cancellationTokenSource?.Dispose();
            }
        }

        public async Task SendOrderAsync(OrderRequest order)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected to server");

            try
            {
                var message = CreateOrderMessage(order);
                await SendMessageAsync(message);
                OnLogMessage($"Order message sent: {order.Symbol}");
            }
            catch (Exception ex)
            {
                OnLogMessage($"Failed to send order: {ex.Message}");
                throw;
            }
        }

        private async Task SendClientIdentificationAsync()
        {
            // Send 16-byte client identification
            var clientName = "APICLIENT       "; // 16 bytes
            byte[] identificationBytes = Encoding.ASCII.GetBytes(clientName);
            
            await _networkStream.WriteAsync(identificationBytes, 0, identificationBytes.Length);
            OnLogMessage("Client identification sent");
        }

        private async Task SendLogicalConnectionRequestAsync()
        {
            var message = CreateLogicalConnectionMessage();
            await SendMessageAsync(message);
            OnLogMessage("Logical connection request (1100) sent");
        }

        private async Task SendLogicalDisconnectionRequestAsync()
        {
            var message = CreateLogicalDisconnectionMessage();
            await SendMessageAsync(message);
            OnLogMessage("Logical disconnection request (1102) sent");
        }

        private FisMessage CreateLogicalConnectionMessage()
        {
            var message = new FisMessage
            {
                RequestNumber = 1100,
                CalledLogicalId = _connectionParams.CalledLogicalId,
                CallingLogicalId = _connectionParams.CallingLogicalId
            };

            // Add user number (3 bytes, padded with zeros)
            message.AddField(0, _connectionParams.UserNumber.PadLeft(3, '0'));
            
            // Add password (16 bytes, padded with spaces)
            message.AddField(1, _connectionParams.Password.PadRight(16, ' '));
            
            // Add 7 byte filler
            message.AddField(7, new string(' ', 7));

            return message;
        }

        private FisMessage CreateLogicalDisconnectionMessage()
        {
            var message = new FisMessage
            {
                RequestNumber = 1102,
                CalledLogicalId = _connectionParams.CalledLogicalId,
                CallingLogicalId = _connectionParams.CallingLogicalId
            };

            // Add user number
            message.AddField(0, _connectionParams.UserNumber.PadLeft(3, '0'));
            
            // Add password
            message.AddField(1, _connectionParams.Password.PadRight(16, ' '));
            
            // Add filler
            message.AddField(2, " ");

            return message;
        }

        private FisMessage CreateOrderMessage(OrderRequest order)
        {
            var message = new FisMessage
            {
                RequestNumber = 2000,
                CalledLogicalId = _connectionParams.CalledLogicalId,
                CallingLogicalId = _connectionParams.CallingLogicalId
            };

            // Add header fields
            message.AddField('B', _connectionParams.UserNumber.PadLeft(5, '0')); // User Number
            message.AddField('C', "O"); // Request Category (Simple order)
            message.AddField('D', "0"); // Command (New order)
            message.AddField('G', order.Symbol); // Stock code
            
            // Add 10 byte filler
            message.AddField(-1, new string(' ', 10));

            // Add order data (bitmap)
            message.AddField(0, order.Side); // Side (0=Buy, 1=Sell)
            message.AddField(1, order.Quantity); // Quantity
            message.AddField(2, "L"); // Modality (Limit)
            message.AddField(3, order.Price); // Price
            message.AddField(4, order.Validity); // Validity (J=Day)
            message.AddField(106, $"0040{_connectionParams.CalledLogicalId.PadLeft(2, '0')}000{_connectionParams.CallingLogicalId.PadLeft(3, '0')}"); // GLID

            return message;
        }

        private async Task SendMessageAsync(FisMessage message)
        {
            try
            {
                byte[] messageBytes = message.ToByteArray();
                await _networkStream.WriteAsync(messageBytes, 0, messageBytes.Length);
                
                OnLogMessage($"Message sent: Request {message.RequestNumber}, Length: {messageBytes.Length}");
                OnLogMessage($"Sent data: {MessageHelper.BytesToHexString(messageBytes, 50)}...");
            }
            catch (Exception ex)
            {
                OnLogMessage($"Failed to send message: {ex.Message}");
                throw;
            }
        }

        private async Task MessageReceivingLoop(CancellationToken cancellationToken)
        {
            var buffer = new byte[32768]; // 32KB buffer
            
            try
            {
                while (!cancellationToken.IsCancellationRequested && _tcpClient.Connected)
                {
                    int bytesRead = await _networkStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    
                    if (bytesRead > 0)
                    {
                        await ProcessReceivedData(buffer, bytesRead);
                    }
                    else
                    {
                        OnLogMessage("Server closed connection");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                OnLogMessage("Message receiving cancelled");
            }
            catch (Exception ex)
            {
                OnLogMessage($"Message receiving error: {ex.Message}");
            }
            finally
            {
                if (_isConnected)
                {
                    _isConnected = false;
                    OnConnectionStatusChanged(false, "Connection lost");
                }
            }
        }

        private async Task ProcessReceivedData(byte[] buffer, int length)
        {
            try
            {
                OnLogMessage($"Raw received data: {MessageHelper.BytesToHexString(buffer, Math.Min(50, length))}...");
                
                // Validate basic message structure
                if (!MessageHelper.ValidateFisMessage(buffer.Take(length).ToArray()))
                {
                    OnLogMessage("Invalid FIS message structure received");
                    return;
                }

                // Parse message
                var header = MessageHelper.ParseHeader(buffer, 2);
                if (header == null)
                {
                    OnLogMessage("Failed to parse message header");
                    return;
                }

                int reqNum = int.Parse(header.RequestNumber);
                OnLogMessage($"Received message: Request {reqNum}, Length: {length}");
                
                // Handle specific message types
                await HandleReceivedMessage(reqNum, buffer, length);
            }
            catch (Exception ex)
            {
                OnLogMessage($"Error processing received data: {ex.Message}");
            }
        }

        private async Task HandleReceivedMessage(int requestNumber, byte[] buffer, int messageLength)
        {
            string messageType = requestNumber switch
            {
                1100 => "Logical Connection Response",
                1102 => "Logical Disconnection Response", 
                2019 => "Order Real Time Message",
                _ => $"Unknown Message ({requestNumber})"
            };

            OnMessageReceived(messageType, $"Request {requestNumber}");

            if (requestNumber == 1100)
            {
                OnLogMessage("Logical connection established successfully");
                
                // Subscribe to real-time messages (2017)
                await SendSubscriptionRequestAsync();
            }
            else if (requestNumber == 2019)
            {
                OnLogMessage("Order update received");
            }
        }

        private async Task SendSubscriptionRequestAsync()
        {
            try
            {
                var message = new FisMessage
                {
                    RequestNumber = 2017,
                    CalledLogicalId = _connectionParams.CalledLogicalId,
                    CallingLogicalId = _connectionParams.CallingLogicalId
                };

                // Subscription parameters (all set to '1' to receive all types)
                message.AddField('E', "1111100"); // E1-E7: ack, reject, exchange reject, trade execution, exchange msg, default, inflected
                message.AddField(-1, new string(' ', 11)); // 11 byte filler

                await SendMessageAsync(message);
                OnLogMessage("Real-time subscription request (2017) sent");
            }
            catch (Exception ex)
            {
                OnLogMessage($"Failed to send subscription request: {ex.Message}");
            }
        }

        private void OnMessageReceived(string messageType, string message)
        {
            MessageReceived?.Invoke(this, new FisMessageEventArgs { MessageType = messageType, Message = message });
        }

        private void OnConnectionStatusChanged(bool isConnected, string statusMessage)
        {
            ConnectionStatusChanged?.Invoke(this, new ConnectionStatusEventArgs { IsConnected = isConnected, StatusMessage = statusMessage });
        }

        private void OnLogMessage(string message)
        {
            LogMessage?.Invoke(this, new LogMessageEventArgs { Message = message });
        }
    }
}