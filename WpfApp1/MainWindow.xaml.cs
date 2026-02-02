// Proveri da su ovi using-zi tu
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Fiksiramo dimenzije
            this.Height = 750;
            this.Width = 600;
            this.ResizeMode = ResizeMode.NoResize;
            CompositionTarget.Rendering += GameLoop;

            CrtajEntitije();
        }

        private void CrtajEntitije()
        {
            Random rand = new Random();
            Random randX = new Random();
            Random randY = new Random();

            int index = rand.Next(0,4);
            double x = randX.Next(300, 560);
            double y = randY.Next(50, 700);

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

            radnaPovrsina.Children.Add(novaLopta.SlikaLopte);
        }
        private DateTime _lastTick = DateTime.Now;

        private void GameLoop(object sender, EventArgs e)
        {
            // Izračunaj koliko je vremena prošlo (u sekundama)
            TimeSpan elapsed = DateTime.Now - _lastTick;
            _lastTick = DateTime.Now;
            double seconds = elapsed.TotalSeconds;

            // Definiši brzinu (pikseli po sekundi)
            double lopticaBrzina = 400; // 400px u sekundi
            double entityBrzina = 100;  // 100px u sekundi

            foreach (var x in radnaPovrsina.Children.OfType<Image>().ToList())
            {
                double top = Canvas.GetTop(x);
                if (double.IsNaN(top)) continue;

                if (x.Name == "loptica")
                {
                    // Pomeraj zavisi od vremena: brzina * sekunde
                    Canvas.SetTop(x, top - (lopticaBrzina * seconds));

                    if (top < -50) radnaPovrsina.Children.Remove(x);
                }
                else if (x.Name == "entity")
                {
                    Canvas.SetTop(x, top + (entityBrzina * seconds));

                    if (top > 750) radnaPovrsina.Children.Remove(x);
                }
            }
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