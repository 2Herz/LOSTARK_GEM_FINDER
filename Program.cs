using System;
using System.Windows.Forms;
using LOSTARK_GEM_FINDER.Core;
using LOSTARK_GEM_FINDER.Services;
using LOSTARK_GEM_FINDER.Views;

namespace LOSTARK_GEM_FINDER
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            IAPIKeyStore APIKeyStore = new ApiKeyStore();
            string APIKey = APIKeyStore.Load();

            if(string.IsNullOrEmpty(APIKey))
            {
                using (var APIKeyForm = new APIKeyForm(APIKeyStore))
                {
                    if (APIKeyForm.ShowDialog() != DialogResult.OK) return;
                    APIKey = APIKeyForm.ApiKey;
                }
            }

            IGemDataRepository DB = new GemDataRepository();
            IAuctionAPI API = new AuctionAPI(APIKey);
            IGemPriceService Service = new GemPriceService(API, DB);

            Application.Run(new MainForm(Service, API, APIKeyStore, DB));
        }
    }
}