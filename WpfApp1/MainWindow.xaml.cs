using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using System.Collections.Generic;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private Igrac mojIgrac;
        private DispatcherTimer stvoriEntiteteTajmer;
        private MediaPlayer soundPlayer = new MediaPlayer();
        private MediaPlayer soundHit = new MediaPlayer();
        private MediaPlayer soundPropaliTeniser = new MediaPlayer();

        // Dodajemo MediaPlayer za pozadinsku muziku
        private MediaPlayer backgroundMusic = new MediaPlayer();

        private DateTime _lastTick = DateTime.Now;

        public MainWindow()
        {
            InitializeComponent();
            this.Height = 750;
            this.Width = 600;
            this.ResizeMode = ResizeMode.NoResize;

            mojIgrac = new Igrac();
            radnaPovrsina.Children.Add(mojIgrac.Igrac1);

            foreach (var srce in mojIgrac.Srca)
            {
                radnaPovrsina.Children.Add(srce);
            }

            CompositionTarget.Rendering += GameLoop;

            // Učitavanje zvučnih efekata
            soundPlayer.Open(new Uri("C:\\Users\\nluka\\OneDrive\\Desktop\\mrezeProjekat\\WpfApp1\\WpfApp1\\src\\pewSfx.mp3"));
            soundHit.Open(new Uri("C:\\Users\\nluka\\OneDrive\\Desktop\\mrezeProjekat\\WpfApp1\\WpfApp1\\src\\umiranje.mp3"));
            soundPropaliTeniser.Open(new Uri("C:\\Users\\nluka\\OneDrive\\Desktop\\mrezeProjekat\\WpfApp1\\WpfApp1\\src\\propaliTeniser.mp3"));

            // --- POZADINSKA MUZIKA ---
            backgroundMusic.Open(new Uri("C:\\Users\\nluka\\OneDrive\\Desktop\\mrezeProjekat\\WpfApp1\\WpfApp1\\src\\Serbia Strong (8 Bit).mp3"));
            backgroundMusic.Volume = 0.2; // Postavlja muziku da bude tiša (0.0 do 1.0)

            // Loop opcija: Kada pesma završi, kreni ispočetka
            backgroundMusic.MediaEnded += (s, e) => {
                backgroundMusic.Position = TimeSpan.Zero;
                backgroundMusic.Play();
            };

            backgroundMusic.Play();
            // --------------------------

            stvoriEntiteteTajmer = new DispatcherTimer();
            stvoriEntiteteTajmer.Interval = TimeSpan.FromSeconds(1.5);
            stvoriEntiteteTajmer.Tick += (s, e) => CrtajEntitije();
            stvoriEntiteteTajmer.Start();
        }

        private void CrtajEntitije()
        {
            Random rand = new Random();
            int index = rand.Next(0, 4);
            double x = rand.Next(20, 540);

            Entity entity = new Entity(index);
            Canvas.SetLeft(entity.SlikaEntity, x);
            Canvas.SetTop(entity.SlikaEntity, -50);

            radnaPovrsina.Children.Add(entity.SlikaEntity);
        }

        private void PlayerMovement(object sender, MouseEventArgs e)
        {
            if (!igraAktivna) return;

            Point position = e.GetPosition(radnaPovrsina);
            Xkoord.Content = Math.Round(position.X);
            Ykoord.Content = Math.Round(position.Y);

            Canvas.SetLeft(mojIgrac.Igrac1, position.X - (mojIgrac.Igrac1.Width / 2));
            Canvas.SetTop(mojIgrac.Igrac1, position.Y - (mojIgrac.Igrac1.Height / 2));
        }

        public void Pucaj(object sender, MouseButtonEventArgs e)
        {
            if (!igraAktivna) return; // Ne pucaj ako je kraj igre

            Lopta novaLopta = new Lopta();
            Point position = e.GetPosition(radnaPovrsina);

            double x = position.X - (novaLopta.SlikaLopte.Width / 2);
            double y = position.Y - (novaLopta.SlikaLopte.Height / 2);

            Canvas.SetLeft(novaLopta.SlikaLopte, x);
            Canvas.SetTop(novaLopta.SlikaLopte, y);
            Panel.SetZIndex(novaLopta.SlikaLopte, 10);

            soundPlayer.Stop();
            soundPlayer.Play();

            radnaPovrsina.Children.Add(novaLopta.SlikaLopte);
        }

        private bool igraAktivna = true;

        private void GameLoop(object sender, EventArgs e)
        {
            if (!igraAktivna) return;

            TimeSpan elapsed = DateTime.Now - _lastTick;
            _lastTick = DateTime.Now;
            double seconds = elapsed.TotalSeconds;

            double lopticaBrzina = 400;
            double entityBrzina = 100;

            var sveSlike = radnaPovrsina.Children.OfType<Image>().ToList();
            Rect playerHitBox = new Rect(Canvas.GetLeft(mojIgrac.Igrac1), Canvas.GetTop(mojIgrac.Igrac1), mojIgrac.Igrac1.Width, mojIgrac.Igrac1.Height);

            foreach (var x in sveSlike)
            {
                double currentTop = Canvas.GetTop(x);
                double currentLeft = Canvas.GetLeft(x);

                if (double.IsNaN(currentTop) || double.IsNaN(currentLeft)) continue;

                if (x.Name == "loptica")
                {
                    double noviTop = currentTop - (lopticaBrzina * seconds);
                    Canvas.SetTop(x, noviTop);
                    Rect lopticaHitBox = new Rect(currentLeft, noviTop, x.Width, x.Height);

                    foreach (var y in sveSlike)
                    {
                        if (y.Name == "entity" || y.Name == "propaliTeniser")
                        {
                            Rect entityHitBox = new Rect(Canvas.GetLeft(y), Canvas.GetTop(y), y.Width, y.Height);
                            if (lopticaHitBox.IntersectsWith(entityHitBox))
                            {
                                PrikaziEksploziju(Canvas.GetLeft(y), Canvas.GetTop(y), (y.Name == "entity") ? 0 : 1);
                                radnaPovrsina.Children.Remove(x);
                                radnaPovrsina.Children.Remove(y);
                                break;
                            }
                        }
                    }
                    if (noviTop < -50) radnaPovrsina.Children.Remove(x);
                }
                else if (x.Name == "entity" || x.Name == "propaliTeniser")
                {
                    double noviTop = currentTop + (entityBrzina * seconds);
                    Canvas.SetTop(x, noviTop);
                    Rect entityHitBox = new Rect(currentLeft, noviTop, x.Width, x.Height);

                    if (entityHitBox.IntersectsWith(playerHitBox))
                    {
                        SmanjiZdravlje();
                        radnaPovrsina.Children.Remove(x);
                        continue;
                    }

                    if (currentTop > 750) radnaPovrsina.Children.Remove(x);
                }
            }
        }

        private void SmanjiZdravlje()
        {
            if (mojIgrac.Health > 0)
            {
                mojIgrac.Health--;
                Image srceZaBrisanje = mojIgrac.Srca[mojIgrac.Health];
                radnaPovrsina.Children.Remove(srceZaBrisanje);

                if (mojIgrac.Health <= 0)
                {
                    KrajIgre();
                }
            }
        }

        private void KrajIgre()
        {
            igraAktivna = false;
            stvoriEntiteteTajmer.Stop();
            backgroundMusic.Stop(); // Opciono: gasi muziku kad izgubiš

            gameOverPanel.Visibility = Visibility.Visible;
            Panel.SetZIndex(gameOverPanel, 100);
            this.Cursor = Cursors.Arrow;
        }

        private void RestartGame(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }

        private void PrikaziEksploziju(double x, double y, int propaliTeniserFlag)
        {
            Image ekspozijaGif = new Image { Width = 60, Height = 60 };
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri("C:\\Users\\nluka\\OneDrive\\Desktop\\mrezeProjekat\\WpfApp1\\WpfApp1\\src\\source.gif");
            bitmap.EndInit();
            ekspozijaGif.Source = bitmap;

            Canvas.SetLeft(ekspozijaGif, x);
            Canvas.SetTop(ekspozijaGif, y);
            radnaPovrsina.Children.Add(ekspozijaGif);

            if (propaliTeniserFlag == 0) { soundHit.Stop(); soundHit.Play(); }
            else { soundPropaliTeniser.Stop(); soundPropaliTeniser.Play(); }

            DispatcherTimer obrisiGif = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            obrisiGif.Tick += (s, e) => { radnaPovrsina.Children.Remove(ekspozijaGif); obrisiGif.Stop(); };
            obrisiGif.Start();
        }
    }
}
//System.Windows.Shapes.Ellipse testKrug = new System.Windows.Shapes.Ellipse();
//testKrug.Width = 20;
//testKrug.Height = 20;
//testKrug.Fill = Brushes.Red;
//Canvas.SetLeft(testKrug, e.GetPosition(radnaPovrsina).X);
//Canvas.SetTop(testKrug, e.GetPosition(radnaPovrsina).Y);
//radnaPovrsina.Children.Add(testKrug);



/*
 private void GameLoop(object sender, EventArgs e)
        {
            // Prolazimo kroz sve elemente na Canvasu
            // Koristimo .ToList() da bismo mogli bezbedno da brišemo objekte iz kolekcije dok prolazimo kroz nju
            foreach (var x in radnaPovrsina.Children.OfType<Image>().ToList())
            {
                // Želimo da pomeramo samo loptice, a ne i igrača
                // Možemo ih razlikovati po tome što igrač ima Name="player"
                if (x.Name == "loptica")
                {
                    // Uzmi trenutnu Y poziciju
                    double trenutniTop = Canvas.GetTop(x);

                    // Pomeri lopticu nagore (smanji Y koordinatu)
                    // Broj 10 određuje brzinu - veći broj, brža loptica
                    Canvas.SetTop(x, trenutniTop - 10);

                    // Ako loptica izleti sa vrha ekrana, obriši je da ne koči kompjuter
                    if (trenutniTop < -50)
                    {
                        radnaPovrsina.Children.Remove(x);
                    }
                }

                if(x.Name == "entity")
                {
                    double trenutniTop = Canvas.GetTop(x);
                    Canvas.SetTop(x, trenutniTop + 0.1);
                    if(trenutniTop > 600)
                    {
                        radnaPovrsina.Children.Remove(x);
                    }
                }
            }
        }
 
 */