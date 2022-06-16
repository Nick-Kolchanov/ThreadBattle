using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ThreadBattle
{
    public partial class Form1 : Form
    {
        Random rnd = new Random();
        AutoResetEvent startEvent = new AutoResetEvent(false);
        Semaphore bulletSemaphore = new Semaphore(3, 3);
        static CancellationTokenSource tokenSource = new CancellationTokenSource();
        CancellationToken token = tokenSource.Token;
        int hit = 0;
        int miss = 0;
        bool isDead = false;
        PictureBox playerTile;

        public Form1()
        {
            InitializeComponent();
            Text = "Thread battle";
            BackColor = Color.DarkCyan;
            Width = 600;
            Height = 400;
            StartGame();
        }

        private void StartGame()
        {
            Score();
            SetPlayerControl();
            EnemyLauncher();
        }

        private async void SetPlayerControl()
        {
            playerTile = new PictureBox()
            {
                Location = new Point(enemyBox.Width / 2 - 25, enemyBox.Height - 80),
                ImageLocation = "playerShip.png",
                SizeMode = PictureBoxSizeMode.StretchImage,
                Size = new Size(50, 50)
            };
            enemyBox.Controls.Add(playerTile);

            while (true)
            {
                foreach (var control in enemyBox.Controls)
                {
                    if (control is PictureBox bullet && (string)bullet.Tag == "enemyBullet")
                    {
                        if (bullet.Bounds.IntersectsWith(playerTile.Bounds))
                        {
                            isDead = true;
                            break;
                        }
                    }
                }

                if (isDead)
                {
                    Score();
                }

                await Task.Delay(100);
            }
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            startEvent.Set();
            switch (e.KeyCode)
            {
                case Keys.Left:
                case Keys.A:
                    if (playerTile.Location.X > 1)
                        playerTile.Location = new Point(playerTile.Location.X - 5, playerTile.Location.Y);
                    break;
                case Keys.Right:
                case Keys.D:
                    if (playerTile.Location.X < enemyBox.Width - 50)
                        playerTile.Location = new Point(playerTile.Location.X + 5, playerTile.Location.Y);
                    break;
                case Keys.Space:
                case Keys.S:
                    StartBullet(true, playerTile.Location.X + 18, playerTile.Location.Y - 5);
                    break;
            }
        }

        private async void StartBullet(bool fromPlayer, int x, int y)
        {
            var bulletTile = new PictureBox()
            {
                Location = new Point(x, y),
                ImageLocation = "bullet.png",
                SizeMode = PictureBoxSizeMode.StretchImage,
                Size = new Size(15, 20),
                Tag = fromPlayer ? "bullet" : "enemyBullet"
            };
            enemyBox.Controls.Add(bulletTile);

            if (fromPlayer)
            {
                if (!bulletSemaphore.WaitOne(0))
                    return;

                while (bulletTile.Location.Y > 0)
                {
                    if (token.IsCancellationRequested)
                        break;

                    bulletTile.Location = new Point(bulletTile.Location.X, bulletTile.Location.Y - 1);
                    await Task.Delay(4);
                }
                
                bulletSemaphore.Release();
            }
            else
            {
                while (bulletTile.Location.Y < enemyBox.Height)
                {
                    if (token.IsCancellationRequested)
                        break;

                    bulletTile.Location = new Point(bulletTile.Location.X, bulletTile.Location.Y + 1);
                    await Task.Delay(4);
                }
            }      

            bulletTile.Dispose();
            enemyBox.Controls.Remove(bulletTile);
        }

        private async void EnemyLauncher()
        {
            await Task.Run(() => startEvent.WaitOne(15000));

            while (true)
            {
                if (token.IsCancellationRequested)
                    break;

                if (rnd.Next(0, 100) < (hit + miss) / 25 + 20)                
                    StartEnemy(rnd.Next(1,200));                

                await Task.Delay(1000);
            }
        }

        private async void StartEnemy(int y)
        {
            // нечетные у появляются слева, четные у появляются справа
            int x = y % 2 == 0 ? 0 : Width;
            // установить направление в зависимости от начальной позиции
            int dir = x == 0 ? 1 : -1;
            dir += dir * (hit + miss) / 10;

            var enemyTile = new PictureBox()
            {
                Location = new Point(x, y),
                SizeMode = PictureBoxSizeMode.StretchImage,
                ImageLocation = "enemyShip.png",
                Size = new Size(50, 50)
            };
            enemyBox.Controls.Add(enemyTile);

            bool isHit = false;
            while ((dir == 1 && enemyTile.Location.X < Width) || (dir == -1 && enemyTile.Location.X > 0))
            {                
                foreach (var control in enemyBox.Controls)
                {
                    if (control is PictureBox bullet && (string)bullet.Tag == "bullet")
                    {
                        if (bullet.Bounds.IntersectsWith(enemyTile.Bounds))
                        {
                            isHit = true;
                            break;
                        }
                    }
                }

                if (isHit)                    
                    break;

                if (token.IsCancellationRequested)
                    break;

                enemyTile.Location = new Point(enemyTile.Location.X + dir, enemyTile.Location.Y);
                if (rnd.Next(1,100) < (hit + miss + 40) / 10)
                {
                    StartBullet(false, enemyTile.Location.X + 20, enemyTile.Location.Y + 50);
                }

                await Task.Delay(40);
            }

            if (isHit)
                Interlocked.Increment(ref hit);
            else
                Interlocked.Increment(ref miss);

            enemyTile.Dispose();
            enemyBox.Controls.Remove(enemyTile);
            Score();
        }

        private void Score()
        {
            if (token.IsCancellationRequested)
                return;

            scoreLabel.Text = $"Попаданий - {hit}, промахов - {miss}";

            if (miss > 30 || isDead)
            {
                tokenSource.Cancel();
                MessageBox.Show("Игра окончена!");
                Application.Exit();                
            }
        }        
    }
}
