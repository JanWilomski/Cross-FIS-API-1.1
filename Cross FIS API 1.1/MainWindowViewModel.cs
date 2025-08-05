// Dodaj te ulepszenia do MainWindowViewModel.cs

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using Cross_FIS_API_1._1.Services;
using Cross_FIS_API_1._1.Commands;
using Cross_FIS_API_1._1.Models;

namespace Cross_FIS_API_1._1
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly FisApiService _fisApiService;
        private string _ipAddress = "172.31.136.4";
        private string _port = "19593";
        private string _user = "103";
        private string _password = "glglgl";
        private string _node = "24300";
        private string _subnode = "14300";
        private string _logText = "";
        private string _statusText = "Ready";
        private string _connectionStatus = "Disconnected";
        private bool _isConnected = false;
        private string _orderSymbol = "FTE";
        private string _orderSide = "0";
        private string _orderQuantity = "100";

        public MainWindowViewModel()
        {
            _fisApiService = new FisApiService();
            _fisApiService.MessageReceived += OnMessageReceived;
            _fisApiService.ConnectionStatusChanged += OnConnectionStatusChanged;
            _fisApiService.LogMessage += OnLogMessage;

            ConnectCommand = new RelayCommand(async () => await ConnectAsync(), () => !string.IsNullOrEmpty(IpAddress));
            SendOrderCommand = new RelayCommand(async () => await SendOrderAsync(), () => IsConnected);
            
            // Initialize with welcome message
            AddLog("FIS API Trading Client initialized");
            AddLog("Ready to connect to FIS SLE server");
        }

        #region Properties
        // ... (existing properties remain the same)
        public string IpAddress
        {
            get => _ipAddress;
            set
            {
                _ipAddress = value;
                OnPropertyChanged();
                ((RelayCommand)ConnectCommand).RaiseCanExecuteChanged();
            }
        }

        public string Port
        {
            get => _port;
            set
            {
                _port = value;
                OnPropertyChanged();
            }
        }

        public string User
        {
            get => _user;
            set
            {
                _user = value;
                OnPropertyChanged();
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
            }
        }

        public string Node
        {
            get => _node;
            set
            {
                _node = value;
                OnPropertyChanged();
            }
        }

        public string Subnode
        {
            get => _subnode;
            set
            {
                _subnode = value;
                OnPropertyChanged();
            }
        }

        public string LogText
        {
            get => _logText;
            set
            {
                _logText = value;
                OnPropertyChanged();
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                _connectionStatus = value;
                OnPropertyChanged();
            }
        }

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                _isConnected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectButtonText));
                ((RelayCommand)SendOrderCommand).RaiseCanExecuteChanged();
            }
        }

        public string ConnectButtonText => IsConnected ? "Disconnect" : "Connect";

        public string OrderSymbol
        {
            get => _orderSymbol;
            set
            {
                _orderSymbol = value;
                OnPropertyChanged();
            }
        }

        public string OrderSide
        {
            get => _orderSide;
            set
            {
                _orderSide = value;
                OnPropertyChanged();
            }
        }

        public string OrderQuantity
        {
            get => _orderQuantity;
            set
            {
                _orderQuantity = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Commands
        public ICommand ConnectCommand { get; }
        public ICommand SendOrderCommand { get; }
        #endregion

        #region Methods
        private async Task ConnectAsync()
        {
            try
            {
                if (IsConnected)
                {
                    AddLog("Disconnecting from server...");
                    StatusText = "Disconnecting...";
                    await _fisApiService.DisconnectAsync();
                }
                else
                {
                    // Validate input parameters
                    if (!ValidateConnectionParameters())
                        return;

                    AddLog("Attempting to connect to FIS SLE server...");
                    StatusText = "Connecting...";
                    
                    var connectionParams = new FisConnectionParameters
                    {
                        IpAddress = IpAddress,
                        Port = int.Parse(Port),
                        UserNumber = User,
                        Password = Password,
                        CalledLogicalId = Node,
                        CallingLogicalId = Subnode
                    };

                    await _fisApiService.ConnectAsync(connectionParams);
                }
            }
            catch (FormatException ex)
            {
                var errorMsg = "Invalid port number format";
                AddLog($"Connection failed: {errorMsg}");
                StatusText = "Connection failed";
                ShowErrorMessage("Connection Error", errorMsg);
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                var errorMsg = $"Network error: {ex.Message}";
                AddLog($"Connection failed: {errorMsg}");
                StatusText = "Connection failed";
                ShowErrorMessage("Network Error", "Unable to connect to server. Please check network connectivity and server address.");
            }
            catch (TimeoutException ex)
            {
                var errorMsg = "Connection timeout";
                AddLog($"Connection failed: {errorMsg}");
                StatusText = "Connection timeout";
                ShowErrorMessage("Timeout Error", "Connection attempt timed out. Please try again.");
            }
            catch (Exception ex)
            {
                var errorMsg = $"Unexpected error: {ex.Message}";
                AddLog($"Connection failed: {errorMsg}");
                StatusText = "Connection failed";
                ShowErrorMessage("Connection Error", errorMsg);
            }
        }

        private async Task SendOrderAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    AddLog("Cannot send order: Not connected to server");
                    ShowErrorMessage("Order Error", "Please connect to server first");
                    return;
                }

                // Validate order parameters
                if (!ValidateOrderParameters())
                    return;

                StatusText = "Sending order...";
                AddLog($"Preparing order: {OrderSymbol} {(OrderSide == "0" ? "BUY" : "SELL")} {OrderQuantity}");
                
                var order = new OrderRequest
                {
                    Symbol = OrderSymbol.Trim().ToUpper(),
                    Side = OrderSide,
                    Quantity = OrderQuantity,
                    OrderType = "L", // Limit order
                    Price = "100.00", // Default price - you may want to make this configurable
                    Validity = "J" // Day order
                };

                await _fisApiService.SendOrderAsync(order);
                AddLog($"Order sent successfully: {OrderSymbol} {(OrderSide == "0" ? "BUY" : "SELL")} {OrderQuantity}");
                StatusText = "Order sent";
            }
            catch (ArgumentException ex)
            {
                var errorMsg = $"Invalid order parameters: {ex.Message}";
                AddLog($"Order failed: {errorMsg}");
                StatusText = "Order failed";
                ShowErrorMessage("Order Error", errorMsg);
            }
            catch (InvalidOperationException ex)
            {
                var errorMsg = $"Operation error: {ex.Message}";
                AddLog($"Order failed: {errorMsg}");
                StatusText = "Order failed";
                ShowErrorMessage("Order Error", errorMsg);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Unexpected error: {ex.Message}";
                AddLog($"Order failed: {errorMsg}");
                StatusText = "Order failed";
                ShowErrorMessage("Order Error", errorMsg);
            }
        }

        private bool ValidateConnectionParameters()
        {
            if (string.IsNullOrWhiteSpace(IpAddress))
            {
                ShowErrorMessage("Validation Error", "Server address is required");
                return false;
            }

            if (!int.TryParse(Port, out int port) || port <= 0 || port > 65535)
            {
                ShowErrorMessage("Validation Error", "Port must be a valid number between 1 and 65535");
                return false;
            }

            if (string.IsNullOrWhiteSpace(User))
            {
                ShowErrorMessage("Validation Error", "User number is required");
                return false;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                ShowErrorMessage("Validation Error", "Password is required");
                return false;
            }

            if (string.IsNullOrWhiteSpace(Node))
            {
                ShowErrorMessage("Validation Error", "Node is required");
                return false;
            }

            if (string.IsNullOrWhiteSpace(Subnode))
            {
                ShowErrorMessage("Validation Error", "Subnode is required");
                return false;
            }

            return true;
        }

        private bool ValidateOrderParameters()
        {
            if (string.IsNullOrWhiteSpace(OrderSymbol))
            {
                ShowErrorMessage("Order Validation Error", "Symbol is required");
                return false;
            }

            if (!int.TryParse(OrderQuantity, out int qty) || qty <= 0)
            {
                ShowErrorMessage("Order Validation Error", "Quantity must be a positive number");
                return false;
            }

            if (OrderSide != "0" && OrderSide != "1")
            {
                ShowErrorMessage("Order Validation Error", "Please select Buy or Sell");
                return false;
            }

            return true;
        }

        private void ShowErrorMessage(string title, string message)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }));
        }

        private void OnMessageReceived(object sender, FisMessageEventArgs e)
        {
            AddLog($"Message received: {e.MessageType} - {e.Message}");
        }

        private void OnConnectionStatusChanged(object sender, ConnectionStatusEventArgs e)
        {
            IsConnected = e.IsConnected;
            ConnectionStatus = e.IsConnected ? "Connected" : "Disconnected";
            StatusText = e.StatusMessage;
            
            if (e.IsConnected)
            {
                AddLog("Successfully connected to FIS SLE server");
            }
            else
            {
                AddLog("Disconnected from FIS SLE server");
            }
        }

        private void OnLogMessage(object sender, LogMessageEventArgs e)
        {
            AddLog(e.Message);
        }

        private void AddLog(string message)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                LogText += $"[{timestamp}] {message}\n";
                
                // Keep log reasonable size (limit to last 500 lines)
                var lines = LogText.Split('\n');
                if (lines.Length > 500)
                {
                    LogText = string.Join("\n", lines[^400..]);
                }
            }));
        }
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}