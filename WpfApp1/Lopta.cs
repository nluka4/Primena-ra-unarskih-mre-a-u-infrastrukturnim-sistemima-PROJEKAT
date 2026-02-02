using System;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace WpfApp1
{
    public class Lopta
    {
        public Image SlikaLopte { get; set; }

        public Lopta()
        {
            SlikaLopte = new Image();
            SlikaLopte.Width = 20;
            SlikaLopte.Height = 20;

            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                // Kosa crta / je obavezna za podfoldere!
                bitmap.UriSource = new Uri("C:\\Users\\nluka\\OneDrive\\Desktop\\mrezeProjekat\\WpfApp1\\WpfApp1\\src\\teniskaLoptica.png");
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                SlikaLopte.Name = "loptica";
                SlikaLopte.Source = bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Greška: " + ex.Message);
            }
        }
    }
}