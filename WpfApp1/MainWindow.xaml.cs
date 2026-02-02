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

        private void GameLoop(object sender, EventArgs e)
        {
            // Prolazimo kroz sve elemente na Canvasu
            // Koristimo .ToList() da bismo mogli bezbedno da brišemo objekte iz kolekcije dok prolazimo kroz nju
            foreach (var x in radnaPovrsina.Children.OfType<Image>().ToList())
            {
                // Želimo da pomeramo samo loptice, a ne i igrača
                // Možemo ih razlikovati po tome što igrač ima Name="player"
                if (x.Name != "player")
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