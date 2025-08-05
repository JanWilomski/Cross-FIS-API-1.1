using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
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
        }

        #region Properties

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
                    await _fisApiService.DisconnectAsync();
                }
                else
                {
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
            catch (Exception ex)
            {
                AddLog($"Connection error: {ex.Message}");
                StatusText = "Connection failed";
            }
        }

        private async Task SendOrderAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    AddLog("Not connected to server");
                    return;
                }

                StatusText = "Sending order...";
                
                var order = new OrderRequest
                {
                    Symbol = OrderSymbol,
                    Side = OrderSide,
                    Quantity = OrderQuantity,
                    OrderType = "L", // Limit order
                    Price = "100.00", // Default price
                    Validity = "J" // Day order
                };

                await _fisApiService.SendOrderAsync(order);
                AddLog($"Order sent: {OrderSymbol} {OrderSide} {OrderQuantity}");
                StatusText = "Order sent";
            }
            catch (Exception ex)
            {
                AddLog($"Order error: {ex.Message}");
                StatusText = "Order failed";
            }
        }

        private void OnMessageReceived(object sender, FisMessageEventArgs e)
        {
            AddLog($"Received: {e.MessageType} - {e.Message}");
        }

        private void OnConnectionStatusChanged(object sender, ConnectionStatusEventArgs e)
        {
            IsConnected = e.IsConnected;
            ConnectionStatus = e.IsConnected ? "Connected" : "Disconnected";
            StatusText = e.StatusMessage;
        }

        private void OnLogMessage(object sender, LogMessageEventArgs e)
        {
            AddLog(e.Message);
        }

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            LogText += $"[{timestamp}] {message}\n";
            
            // Keep log reasonable size
            var lines = LogText.Split('\n');
            if (lines.Length > 1000)
            {
                LogText = string.Join("\n", lines[^500..]);
            }
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