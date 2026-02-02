// Proveri da su ovi using-zi tu
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace WpfApp1
{
    public partial class MainWindow : Window
    {

        private MediaPlayer soundPlayer = new MediaPlayer();
        private MediaPlayer soundHit = new MediaPlayer();
        private MediaPlayer soundPropaliTeniser = new MediaPlayer();
        public MainWindow()
        {
            InitializeComponent();
            // Fiksiramo dimenzije
            this.Height = 750;
            this.Width = 600;
            this.ResizeMode = ResizeMode.NoResize;
            CompositionTarget.Rendering += GameLoop;
            soundPlayer.Open(new Uri("C:\\Users\\nluka\\OneDrive\\Desktop\\mrezeProjekat\\WpfApp1\\WpfApp1\\src\\pewSfx.mp3", UriKind.Relative));
            soundHit.Open(new Uri("C:\\Users\\nluka\\OneDrive\\Desktop\\mrezeProjekat\\WpfApp1\\WpfApp1\\src\\umiranje.mp3", UriKind.Relative)); // Dodaj svoj fajl
            soundPropaliTeniser.Open(new Uri("C:\\Users\\nluka\\OneDrive\\Desktop\\mrezeProjekat\\WpfApp1\\WpfApp1\\src\\propaliTeniser.mp3", UriKind.Relative)); // Dodaj svoj fajl

            int health = 3;
            int healthWidth = 450; 
            for(int i = 0; i < 3; i++)
            {
                Image healthContainer = new Image();
                healthContainer.Width = 25;
                healthContainer.Height = 25;
                BitmapImage hearth = new BitmapImage();
                hearth.BeginInit();
                hearth.UriSource = new Uri("C:\\Users\\nluka\\OneDrive\\Desktop\\mrezeProjekat\\WpfApp1\\WpfApp1\\src\\health.png");
                hearth.CacheOption = BitmapCacheOption.OnLoad;
                hearth.EndInit();
                healthContainer.Source = hearth;

                Canvas.SetLeft(healthContainer, healthWidth += 30);
                Canvas.SetTop(healthContainer, 5);
                radnaPovrsina.Children.Add(healthContainer);
            }
            DispatcherTimer stvoriEntiteteTajmer = new DispatcherTimer();
            stvoriEntiteteTajmer.Interval = TimeSpan.FromSeconds(1.5);
            stvoriEntiteteTajmer.Tick += (s, e) => CrtajEntitije(); // Poziva tvoju metodu
            stvoriEntiteteTajmer.Start();

        }

        private void CrtajEntitije()
        {
            Random rand = new Random();
            Random randX = new Random();

            int index = rand.Next(0,4);
            double x = randX.Next(0, 560);
            double y = 0;

            Entity entity = new Entity(index);

            Canvas.SetLeft(entity.SlikaEntity,x-(entity.SlikaEntity.Width/2));
            Canvas.SetTop(entity.SlikaEntity, y - (entity.SlikaEntity.Height / 2));

            radnaPovrsina.Children.Add(entity.SlikaEntity);
        }

        // MouseMove zahteva MouseEventArgs
        private void PlayerMovement(object sender, MouseEventArgs e)
        {
            Point position = e.GetPosition(radnaPovrsina);
            Xkoord.Content = Math.Round(position.X);
            Ykoord.Content = Math.Round(position.Y);

            Canvas.SetLeft(player, position.X - (player.Width / 2));
            Canvas.SetTop(player, position.Y - (player.Height / 2));
        }

        // MouseDown ZAHTEVA MouseButtonEventArgs (ovde ti je bila greška CS0123)
        public void Pucaj(object sender, MouseButtonEventArgs e)
        {
            Lopta novaLopta = new Lopta();
            Point position = e.GetPosition(radnaPovrsina);

            // Pozicioniranje
            double x = position.X - (novaLopta.SlikaLopte.Width / 2);
            double y = position.Y - (novaLopta.SlikaLopte.Height / 2);

            Canvas.SetLeft(novaLopta.SlikaLopte, x);
            Canvas.SetTop(novaLopta.SlikaLopte, y);
            Panel.SetZIndex(novaLopta.SlikaLopte, 10);

            soundPlayer.Stop(); // Zaustavlja prethodni ako još traje
            soundPlayer.Play();

            radnaPovrsina.Children.Add(novaLopta.SlikaLopte);
        }
        private DateTime _lastTick = DateTime.Now;

        private void GameLoop(object sender, EventArgs e)
        {
            // 1. Vremenska kalkulacija
            TimeSpan elapsed = DateTime.Now - _lastTick;
            _lastTick = DateTime.Now;
            double seconds = elapsed.TotalSeconds;

            double lopticaBrzina = 400;
            double entityBrzina = 100;

            // Uzimamo kopiju liste dece Canvasa da izbegnemo greške pri brisanju
            var sveSlike = radnaPovrsina.Children.OfType<Image>().ToList();

            foreach (var x in sveSlike)
            {
                // Dobijamo trenutne koordinate (Popravka za CS0103)
                double currentTop = Canvas.GetTop(x);
                double currentLeft = Canvas.GetLeft(x);

                if (double.IsNaN(currentTop) || double.IsNaN(currentLeft)) continue;

                // --- LOGIKA ZA LOPTICU ---
                if (x.Name == "loptica")
                {
                    double noviTop = currentTop - (lopticaBrzina * seconds);
                    Canvas.SetTop(x, noviTop);

                    // Kreiramo HitBox za lopticu (Pravougaonik koji je prati)
                    Rect lopticaHitBox = new Rect(currentLeft + 5, noviTop + 5, x.Width - 10 ,x.Height - 10);

                    // Proveravamo da li je ova loptica udarila bilo koji entitet
                    foreach (var y in sveSlike)
                    {
                        if (y.Name == "entity" || y.Name == "propaliTeniser")
                        {
                            double eTop = Canvas.GetTop(y);
                            double eLeft = Canvas.GetLeft(y);

                            // HitBox za neprijatelja
                            Rect entityHitBox = new Rect(eLeft + 10,eTop + 10,y.Width - 20,y.Height - 20);

                            // Provera sudara
                            if (lopticaHitBox.IntersectsWith(entityHitBox))
                            {
                                if (y.Name == "entity")
                                {
                                    PrikaziEksploziju(eLeft,eTop,0);

                                }
                                else
                                {
                                    PrikaziEksploziju(eLeft, eTop, 1);
                                }
                                radnaPovrsina.Children.Remove(x); // Brisi lopticu
                                radnaPovrsina.Children.Remove(y); // Brisi entitet
                                                                  // Ovde možeš dodati: score++;
                                
                                break;
                            }
                        }
                    }

                    if (noviTop < -50) radnaPovrsina.Children.Remove(x);
                }
                // --- LOGIKA ZA ENTITET ---
                else if (x.Name == "entity" || x.Name == "propaliTeniser")
                {
                    Canvas.SetTop(x, currentTop + (entityBrzina * seconds));
                    if (currentTop > 750) radnaPovrsina.Children.Remove(x);
                }
            }
        }

        private void PrikaziEksploziju(double x, double y,int propaliTeniserFlag)
        {
            Image ekspozijaGif = new Image();
            ekspozijaGif.Width = 60;
            ekspozijaGif.Height = 60;

            // Učitavanje GIF-a (Moraš imati gif u src folderu)
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri("C:\\Users\\nluka\\OneDrive\\Desktop\\mrezeProjekat\\WpfApp1\\WpfApp1\\src\\source.gif");
            bitmap.EndInit();
            ekspozijaGif.Source = bitmap;

            // Pozicioniranje na mestu sudara
            Canvas.SetLeft(ekspozijaGif, x);
            Canvas.SetTop(ekspozijaGif, y);
            radnaPovrsina.Children.Add(ekspozijaGif);

            // Zvuk eksplozije
            if (propaliTeniserFlag == 0) { 
                soundHit.Stop();
                soundHit.Play();
            } else
            {
                soundPropaliTeniser.Stop();
                soundPropaliTeniser.Play();
            }

            // Tajmer koji briše GIF nakon 1 sekunde (da ne ostane na ekranu)
            DispatcherTimer obrisiGif = new DispatcherTimer();
            obrisiGif.Interval = TimeSpan.FromSeconds(1);
            obrisiGif.Tick += (s, e) => {
                radnaPovrsina.Children.Remove(ekspozijaGif);
                obrisiGif.Stop();
            };
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