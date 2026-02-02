using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace WpfApp1
{
    internal class Igrac
    {
        public Image Igrac1 { get; set; }
        public int Health { get; set; }
        public List<Image> Srca { get; set; } = new List<Image>();

        public Igrac()
        {
            Health = 3;
            Srca = new List<Image>();

            int healthWidth = 450;
            for (int i = 0; i < 3; i++)
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
                Srca.Add(healthContainer); // Čuvamo srce u listi
            }

            Igrac1 = new Image();
            Igrac1.Width = 100; // Vraćamo na originalnu veličinu sa tvog XAML-a
            Igrac1.Height = 64;
            Igrac1.Name = "player"; // Zadržavamo name zbog logike u GameLoop-u

            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri("C:\\Users\\nluka\\OneDrive\\Desktop\\mrezeProjekat\\WpfApp1\\WpfApp1\\src\\igrac.png");
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                Igrac1.Source = bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Greška: " + ex.Message);
            }
        }
    }
}
