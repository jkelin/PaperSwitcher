using PaperSwitcher.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PaperSwitcher
{
    public partial class Form1 : Form
    {
        DirectoryInfo WallpaperFolder;
        bool WallpaperFolderGood = false;
        bool Running = false;
        TimeSpan Interval = TimeSpan.Zero;
        int MonitorCount => Screen.AllScreens.Count();
        FileInfo[] Current = new FileInfo[0];
        HashSet<string> Seen = new HashSet<string>();

        Settings Config => Settings.Default;

        public Form1()
        {
            InitializeComponent();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                textBox1.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Config.Seen = Config.Seen ?? new System.Collections.Specialized.StringCollection();
            MinimumSize = Size;

            foreach (var item in Config.Seen)
            {
                Seen.Add(item);
            }

            textBox1.Text = Config.Path;
            comboBox1.SelectedIndex = Config.SelectedInterval;

            if (WallpaperFolderGood)
            {
                WindowState = FormWindowState.Minimized;
                Hide();
                StartRunning();
            }

            ReloadEnabled();
        }

        void ReloadEnabled()
        {
            if (!WallpaperFolderGood) StopRunning();
            btnStart.Enabled = comboBox1.Enabled = WallpaperFolderGood;
            label1.Enabled = btnNext.Enabled = Running;
            btnStart.Text = Running ? "Stop" : "Start";
            this.Text = notifyIcon1.Text = Running ? "PaperSwitcher - Running" : "PaperSwitcher";
        }

        void StartRunning()
        {
            Next();
            Redisplay();
            Running = true;
        }

        void StopRunning()
        {
            Running = false;
        }

        void ScalePic(Image pic, Graphics target, int width, int height)
        {
            var ratio = pic.Height * 1.0 / pic.Width;

            int outH = height;
            int outW = (int)(outH / ratio);

            if (outW < width)
            {
                outW = width;
                outH = (int)(width * ratio);
            }

            var offsetX = (outW - width) / 2;
            var offsetY = (outH - height) / 2;

            var rect = new Rectangle(-offsetX, -offsetY, outW, outH);
            target.DrawImage(pic, rect);
        }

        void Redisplay()
        {
            label1.Text = string.Join("\n", Current.Select(f => f.Name));
            var xMin = Screen.AllScreens.Min(s => s.Bounds.Left);
            var yMin = Screen.AllScreens.Min(s => s.Bounds.Top);
            var xMax = Screen.AllScreens.Max(s => s.Bounds.Right);
            var yMax = Screen.AllScreens.Max(s => s.Bounds.Bottom);

            var filename = Path.GetTempFileName() + ".bmp";

            using (var img = new Bitmap(xMax - xMin, yMax - yMin))
            {
                using (var g = Graphics.FromImage(img))
                {
                    for (int i = 0; i < Screen.AllScreens.Length; i++)
                    {
                        var screen = Screen.AllScreens[i];
                        using (Image scaled = new Bitmap(screen.Bounds.Width, screen.Bounds.Height))
                        {
                            using (var sg = Graphics.FromImage(scaled))
                            {
                                try
                                {
                                    var pic = Image.FromFile(Current[i].FullName);
                                    ScalePic(pic, sg, screen.Bounds.Width, screen.Bounds.Height);
                                }
                                catch { }
                            }

                            var rect = new Rectangle(screen.Bounds.Left - xMin, screen.Bounds.Top - yMin, screen.Bounds.Width, screen.Bounds.Height);
                            g.DrawImage(scaled, rect);
                        }
                    }
                }
                img.Save(filename);
            }

            Wallpaper.Set(filename, Wallpaper.Style.Tiled);
        }

        FileInfo[] GetImagesIn(DirectoryInfo path)
        {
            return WallpaperFolder.GetFiles("*.jpg")
                .Concat(WallpaperFolder.GetFiles("*.jpeg"))
                .Concat(WallpaperFolder.GetFiles("*.png"))
                .Concat(WallpaperFolder.GetFiles("*.bmp"))
                .OrderBy(i => i.Name)
                .ToArray();
        }

        void Next()
        {
            var images = GetImagesIn(WallpaperFolder);

            if(Seen.Count + MonitorCount > images.Length)
            {
                Seen.Clear();
                Config.Seen.Clear();
            }

            int left = MonitorCount;
            Current = new FileInfo[MonitorCount];
            Random r = new Random();

            while (left != 0)
            {
                var img = images[r.Next(0, images.Length - 1)];
                if (Seen.Contains(img.Name)) continue;

                Current[left - 1] = img;
                Seen.Add(img.Name);
                Config.Seen.Add(img.Name);
                left--;
            }

            Config.Save();
            Redisplay();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (Config.Path != textBox1.Text)
            {
                Config.Path = textBox1.Text;
                Config.Save();
            }

            try
            {
                WallpaperFolder = new DirectoryInfo(textBox1.Text);
                if (!WallpaperFolder.Exists) throw new Exception();
                if (GetImagesIn(WallpaperFolder).Length < MonitorCount) throw new Exception();
                WallpaperFolderGood = true;
            }
            catch (Exception)
            {
                WallpaperFolderGood = false;
            }

            ReloadEnabled();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Config.SelectedInterval != comboBox1.SelectedIndex)
            {
                Config.SelectedInterval = comboBox1.SelectedIndex;
                Config.Save();
            }

            var splat = comboBox1.SelectedItem.ToString().Split(' ');
            var period = int.Parse(splat[0]);
            var name = splat[1];

            switch (name)
            {
                case "second":
                case "seconds":
                    Interval = new TimeSpan(0, 0, period);
                    break;

                case "minute":
                case "minutes":
                    Interval = new TimeSpan(0, period, 0);
                    break;

                case "hour":
                case "hours":
                    Interval = new TimeSpan(period, 0, 0);
                    break;

                default:
                    throw new IndexOutOfRangeException();
            }

            timer1.Stop();
            timer1.Interval = (int)(Interval.TotalMilliseconds);
            timer1.Start();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            Running = !Running;
            ReloadEnabled();
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            Next();
        }

        private void notifyIcon1_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            BringToFront();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized) Hide();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (!Running) return;

            Next();
        }

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            contextMenuStrip1.Show(MousePosition);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void nextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Next();
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            BringToFront();
        }
    }
}
