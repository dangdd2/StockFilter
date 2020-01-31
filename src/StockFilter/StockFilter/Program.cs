using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RestSharp.Serialization.Json;

namespace StockFilter
{
    class Program
    {
        #region MyRegion
        static List<string> vn30 = new List<string>()
        {
            "BID",
            "BVH",
            "CTD",
            "CTG",
            "DPM",
            "EIB",
            "FPT",
            "GAS",
            "GMD",
            "HDB",
            "HPG",
            "MBB",
            "MSN",
            "MWG",
            "NVL",
            "PNJ",
            "REE",
            "ROS",
            "SAB",
            "SBT",
            "SSI",
            "STB",
            "TCB",
            "VCB",
            "VHM",
            "VIC",
            "VJC",
            "VNM",
            "VPB",
            "VRE"
        };

        static List<string> others = new List<string>()
        {
            "POW",
            "BSR"
        };
        #endregion
        static void Main(string[] args)
        {
            var cache = new JsonSerializer();
            var fireAntClient = new FireAntClient(cache,new ErrorLogger());
            var data = new List<StockItem>();

            
            var stockList = vn30.Union(others).Distinct();

            foreach (var symbol in stockList)
            {
                data.Add(fireAntClient.GetStockInfo(symbol));
            }
            
            
            ImportToSQLServer(data);

            Console.WriteLine(" -- DONE -- ");
            Console.ReadLine();
        }

        private static void ImportToSQLServer(List<StockItem> data)
        {
            var type = Type.GetType("StockFilter.StockItem");

            var table = new Table(type, "");
            table.Create();

            table.Insert(data);
        }
    }
}
