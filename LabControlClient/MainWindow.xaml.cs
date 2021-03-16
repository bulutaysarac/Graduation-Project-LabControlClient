using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using file_transfer;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Interop;
using System.Diagnostics;

namespace LabControlClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        System.Timers.Timer controllerTimer = new System.Timers.Timer();

        #region variables

        private KeyboardHook keyboardHook = null;
        private TcpListener commandListener;
        private TcpClient commandClient;
        private Thread threadListen;

        #endregion

        #region file_transfering_variables

        private List<TransferQueue> fileTransfers = new List<TransferQueue>();
        private Listener fileListener;
        private TransferClient fileClient;
        private string outputFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        private bool IsServerRunning;

        #endregion

        #region screen_sharing_variables

        private System.Windows.Forms.Timer screenTimer = new System.Windows.Forms.Timer() { Interval = 120 };
        private TcpClient screenClient;
        private NetworkStream screenMainStream;

        [StructLayout(LayoutKind.Sequential)]
        struct CURSORINFO
        {
            public Int32 cbSize;
            public Int32 flags;
            public IntPtr hCursor;
            public POINTAPI ptScreenPos;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINTAPI
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll")]
        static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll")]
        static extern bool DrawIcon(IntPtr hDC, int X, int Y, IntPtr hIcon);

        const Int32 CURSOR_SHOWING = 0x00000001;

        #endregion

        #region window_close_button_disable

        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        #endregion

        #region double_click_disable

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            System.Windows.Interop.HwndSource source = System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            source.AddHook(new System.Windows.Interop.HwndSourceHook(WndProc));
        }

        private int WM_NCLBUTTONDBLCLK { get { return 0x00A3; } }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCLBUTTONDBLCLK)
                handled = true;

            return IntPtr.Zero;
        }

        #endregion

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            controllerTimer.Interval = 100;
            controllerTimer.Elapsed += ControllerTimer_Elapsed;
            controllerTimer.Start();

            this.SourceInitialized += this.MainWindow_SourceInitialized;
            this.screenTimer.Tick += this.ScreenTimer_Tick;

            DataClass.mainWindow = this;
            this.LockScreen(true);
            this.fileListener = new Listener();
            this.fileListener.Accepted += this.Listener_Accepted;

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);

            this.threadListen = new Thread(this.ListenForCommand);
            this.threadListen.Start();

            this.StartFileServer();
        }

        private void ControllerTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                if (Process.GetProcessesByName("BackgroundService").Length == 0)
                    Process.Start(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\BackgroundService.exe");
            }
            catch { }
        }

        #region screen_share

        private void ScreenTimer_Tick(object sender, EventArgs e)
        {
            this.SendDesktopImage();
        }

        private System.Drawing.Image GrabDesktop()
        {
            System.Drawing.Rectangle bounds = Screen.PrimaryScreen.Bounds;
            Bitmap screenShot = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics graphic = Graphics.FromImage(screenShot);
            graphic.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            CURSORINFO pci;
            pci.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(CURSORINFO));

            if (GetCursorInfo(out pci))
            {
                if (pci.flags == CURSOR_SHOWING)
                {
                    DrawIcon(graphic.GetHdc(), pci.ptScreenPos.x, pci.ptScreenPos.y, pci.hCursor);
                    graphic.ReleaseHdc();
                }
            }

            ImageCodecInfo jgpEncoder = GetEncoder(ImageFormat.Jpeg);
            System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;
            EncoderParameters myEncoderParameters = new EncoderParameters(1);
            EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, 40L);
            myEncoderParameters.Param[0] = myEncoderParameter;

            MemoryStream stream = new MemoryStream();
            screenShot.Save(stream, jgpEncoder, myEncoderParameters);
            screenShot = new Bitmap(stream);

            return screenShot;
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();

            foreach (ImageCodecInfo codec in codecs)
                if (codec.FormatID == format.Guid)
                    return codec;

            return null;
        }

        private void SendDesktopImage()
        {
            try
            {
                BinaryFormatter imageBinaryFormatter = new BinaryFormatter();
                this.screenMainStream = this.screenClient.GetStream();
                imageBinaryFormatter.Serialize(this.screenMainStream, GrabDesktop());
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message);
            }
        }

        public void ConnectScreenShareServer()
        {
            try
            {
                this.screenClient = new TcpClient();
                this.screenClient.Connect(DataClass.screenShareIP, 1919);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message);
            }
        }

        public void DisconnectScreenShareServer()
        {
            this.screenClient = null;
        }

        public void StartSharingScreen()
        {
            this.screenTimer.Start();
        }

        public void StopSharingScreen()
        {
            this.screenTimer.Stop();
        }

        #endregion

        #region file_transfering

        private void StartFileServer()
        {
            if (this.IsServerRunning)
                return;
            this.IsServerRunning = true;
            try
            {
                this.fileListener.Start(1818);
            }
            catch
            {
                System.Windows.MessageBox.Show("Unable to listen on port 1818", "");
            }
        }

        private void RegisterEvents()
        {
            this.fileClient.Complete += this.TransferClient_Complete;
            this.fileClient.Disconnected += this.TransferClient_Disconnected;
            this.fileClient.ProgressChanged += this.TransferClient_ProgressChanged;
            this.fileClient.Queued += this.TransferClient_Queued;
            this.fileClient.Stopped += this.TransferClient_Stopped;
        }

        private void DeregisterEvents()
        {
            if (this.fileClient == null)
                return;

            this.fileClient.Complete -= this.TransferClient_Complete;
            this.fileClient.Disconnected -= this.TransferClient_Disconnected;
            this.fileClient.ProgressChanged -= this.TransferClient_ProgressChanged;
            this.fileClient.Queued -= this.TransferClient_Queued;
            this.fileClient.Stopped -= this.TransferClient_Stopped;
        }

        private void Listener_Accepted(object sender, SocketAcceptedEventArgs e)
        {
            if (!this.Dispatcher.CheckAccess())
            {
                this.Dispatcher.Invoke(new SocketAcceptedHandler(this.Listener_Accepted), sender, e);
                return;
            }

            this.fileListener.Stop();
            this.fileClient = new TransferClient(e.Accepted);
            this.fileClient.OutputFolder = this.outputFolder;
            this.RegisterEvents();
            this.fileClient.Run();
        }

        private void TransferClient_Stopped(object sender, TransferQueue queue)
        {
            if (!this.Dispatcher.CheckAccess())
            {
                this.Dispatcher.Invoke(new TransferEventHandler(this.TransferClient_Stopped), sender, queue);
                return;
            }

            this.fileTransfers.Remove(queue);
        }

        private void TransferClient_Queued(object sender, TransferQueue queue)
        {
            if (!this.Dispatcher.CheckAccess())
            {
                this.Dispatcher.Invoke(new TransferEventHandler(this.TransferClient_Queued), sender, queue);
                return;
            }

            this.fileTransfers.Add(queue);

            if (queue.Type == QueueType.Download)
                this.fileClient.StartTransfer(queue);
        }

        private void TransferClient_ProgressChanged(object sender, TransferQueue queue)
        {
            if (!this.Dispatcher.CheckAccess())
            {
                this.Dispatcher.Invoke(new TransferEventHandler(this.TransferClient_ProgressChanged), sender, queue);
                return;
            }
        }

        private void TransferClient_Disconnected(object sender, EventArgs e)
        {
            if (!this.Dispatcher.CheckAccess())
            {
                this.Dispatcher.Invoke(new EventHandler(this.TransferClient_Disconnected), sender, e);
                return;
            }

            this.DeregisterEvents();

            foreach (TransferQueue transferQueue in this.fileTransfers)
                if(transferQueue != null)
                    transferQueue.Close();

            //Set the client to null
            this.fileClient = null;

            //If the server is still running, wait for another connection
            if (this.IsServerRunning)
                this.fileListener.Start(1818);
        }

        private void TransferClient_Complete(object sender, TransferQueue queue)
        {
            System.Media.SystemSounds.Asterisk.Play();
        }

        #endregion

        /// <summary>
        /// Listening for commands which are coming from main machine.
        /// </summary>
        public void ListenForCommand()
        {
            this.commandListener = new TcpListener(IPAddress.Any, 1717);
            this.commandListener.Start();
            this.commandClient = this.commandListener.AcceptTcpClient();
            NetworkStream nwStream = this.commandClient.GetStream();
            byte[] buffer = new byte[this.commandClient.ReceiveBufferSize];
            int bytesRead = nwStream.Read(buffer, 0, this.commandClient.ReceiveBufferSize);
            string dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            DataClass.ReceivedData = dataReceived;
            this.commandClient.Close();
            this.commandListener.Stop();
            this.ListenForCommand();
        }

        /// <summary>
        /// Preventing from ALT+F4
        /// </summary>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
        }

        /// <summary>
        /// Locking screen.
        /// </summary>
        public void LockScreen(bool lockScreen)
        {
            try
            {
                if (lockScreen)
                {
                    this.WindowState = WindowState.Maximized;
                    this.LockKeyboard(true);
                    Taskbar.Hide();
                }
                else
                {
                    this.WindowState = WindowState.Minimized;
                    this.LockKeyboard(false);
                    Taskbar.Show();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// Locking keyboard.
        /// </summary>
        public void LockKeyboard(bool lockKeyboard)
        {
            if (lockKeyboard)
                this.keyboardHook = new KeyboardHook();
            else
                this.keyboardHook.Dispose();
        }
    }
}
