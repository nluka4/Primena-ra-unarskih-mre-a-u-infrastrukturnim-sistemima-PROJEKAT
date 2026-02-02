using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace WpfApp1
{
    internal class Entity
    {
        public Image SlikaEntity { get; set; }
        public Image Napad { get; set; }
        public Image Loot { get; set; }
        public Image Poof { get; set; }

        public Entity (int index)
        {
            SlikaEntity = new Image();
            SlikaEntity.Width = 200; 
            SlikaEntity.Height = 200;

            string[] slikaEntityNiz = { "C:\\Users\\nluka\\OneDrive\\Desktop\\mrezeProjekat\\WpfApp1\\WpfApp1\\src\\alcarazLobanja.png",
                "C:\\Users\\nluka\\OneDrive\\Desktop\\mrezeProjekat\\WpfApp1\\WpfApp1\\src\\federerLobanja.png" ,
                "C:\\Users\\nluka\\OneDrive\\Desktop\\mrezeProjekat\\WpfApp1\\WpfApp1\\src\\nadalLobanja.png" ,
                "C:\\Users\\nluka\\OneDrive\\Desktop\\mrezeProjekat\\WpfApp1\\WpfApp1\\src\\retard.png",
                "C:\\Users\\nluka\\OneDrive\\Desktop\\mrezeProjekat\\WpfApp1\\WpfApp1\\src\\sinnerDrogospng.png"};
            try
            {
                BitmapImage bitmapEntity = new BitmapImage();
                bitmapEntity.BeginInit();
                bitmapEntity.UriSource = new Uri(slikaEntityNiz[index]);
                bitmapEntity.CacheOption = BitmapCacheOption.OnLoad;
                bitmapEntity.EndInit();

                SlikaEntity.Name = "entity";
                if(index == 3)
                {
                    SlikaEntity.Name = "propaliTeniser";
                }
                SlikaEntity.Source = bitmapEntity;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Greska :" +  ex.Message);
            }
        }
    }
}
