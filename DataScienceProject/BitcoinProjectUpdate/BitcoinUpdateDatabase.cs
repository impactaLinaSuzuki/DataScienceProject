﻿using BitcoinProject.Configuration;
using BitcoinProject.Interfaces;
using BitcoinProject.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Timers;

namespace BitcoinProjectUpdate
{
    public partial class BitcoinUpdateDatabase : ServiceBase
    {
        private Timer updateTimer;
        //private readonly IBitcoinOptions _BitcoinOptions;
        //private readonly IConnectDatabaseService _ConnectDatabaseServce;
        //private readonly IBitcoinOptions _BitcoinOptions;
        //private readonly IDatabaseOptions _DatabaseOptions;
        //private readonly IBitcoinProjectService _BitcoinProjectService;
        //private readonly IConnectDatabaseService _ConnectDatabaseServce;
        //private readonly IQueryBitcoinDataService _QueryBitcoinDataService;


        public BitcoinUpdateDatabase(
            //IBitcoinOptions bitcoinOptions,
            //IDatabaseOptions databaseOptions,
            //IBitcoinProjectService bitcoinProjectService,
            //IConnectDatabaseService connectDatabaseServce
            //IQueryBitcoinDataService queryBitcoinDataService
            )
        {
            InitializeComponent();
            //_BitcoinOptions = bitcoinOptions;
            //_DatabaseOptions = databaseOptions;
            //_BitcoinProjectService = bitcoinProjectService;
            //_ConnectDatabaseServce = connectDatabaseServce;
            //_QueryBitcoinDataService = queryBitcoinDataService;
        }

        protected override void OnStart(string[] args)
        {
            // hora de agendamento para atualizar
            //double intervalMilliseconds = 24 * 60 * 60 * 1000; // 24 horas
            //updateTimer = new Timer(intervalMilliseconds);
            //updateTimer.Elapsed += UpdateTimer_Elapsed;
            //updateTimer.Start();

            // a cada 1 minuto
            updateTimer = new Timer();
            updateTimer.Interval = 60000;
            updateTimer.Elapsed += UpdateTimer_Elapsed;
            updateTimer.Start();
        }

        protected override void OnStop()
        {
            updateTimer.Stop();
            updateTimer.Dispose();
        }

        private async void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            await CallApi();
        }

        private async Task CallApi()
        {
            try
            {
                var ohlcvList = new List<Ohlcv>();
                var apiUrl = $"v1/ohlcv/BITSTAMP_SPOT_BTC_USD/history?period_id=1DAY&limit=1825";
                var client2 = new RestClient("https://rest.coinapi.io");
                var request = new RestRequest(apiUrl, Method.Get);

                request.AddHeader("X-CoinAPI-Key", "2E0881BD-0F53-412B-B6D8-B448194306B4");

                try
                {
                    MongoClient mongoClient = new MongoClient("mongodb://localhost:27017");
                    IMongoDatabase db = mongoClient.GetDatabase("bitcoin");
                    var collection = db.GetCollection<BsonDocument>("bitcoinCollection");

                    var lastRecord = collection.Find(Builders<BsonDocument>.Filter.Empty)
                                   .SortByDescending(d => d["time_period_end"])
                                   .Limit(1)
                                   .FirstOrDefault();

                    var lastRecordDate = lastRecord != null ? lastRecord["time_period_end"].ToString() : null;

                    // Calculate the start date for the API request
                    var startDate = lastRecordDate != null
                                    ? DateTime.Parse(lastRecordDate).AddDays(1).ToString("yyyy-MM-ddTHH:mm:ss")
                                    : DateTime.UtcNow.AddYears(-5).ToString("yyyy-MM-ddTHH:mm:ss");

                    RestResponse response = client2.Execute(request);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        var content = response.Content;
                        ohlcvList = JsonConvert.DeserializeObject<List<Ohlcv>>(content);
                    }

                    foreach (var ohlcv in ohlcvList)
                    {
                        TimeZoneInfo tzBrasil = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
                        DateTime lastUpdate = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tzBrasil);

                        ohlcv.LastUpdate = lastUpdate;

                        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("time_period_start", ohlcv.time_period_start);

                        UpdateOptions updateOptions = new UpdateOptions
                        {
                            // Se o documento não existir, ele será inserido
                            IsUpsert = true
                        };
                        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update
                            .Set("time_period_start", ohlcv.time_period_start)
                            .Set("time_period_end", ohlcv.time_period_end)
                            .Set("time_open", ohlcv.time_open)
                            .Set("time_close", ohlcv.time_close)
                            .Set("price_open", ohlcv.price_open)
                            .Set("price_high", ohlcv.price_high)
                            .Set("price_low", ohlcv.price_low)
                            .Set("price_close", ohlcv.price_close)
                            .Set("volume_traded", ohlcv.volume_traded)
                            .Set("trades_count", ohlcv.trades_count)
                            .Set("LastUpdate", lastUpdate);

                        collection.UpdateOne(filter, update, updateOptions);
                    }

                    Console.WriteLine("Bitcoin data has been inserted into MongoDB.");
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message, ex);
                }

                //return ohlcvList;
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync(ex.Message);
            }
            finally
            {
                await Console.Out.WriteLineAsync("Fim da atualização");
            }
        }

        //private List<Ohlcv> _SelectBictoinData()
        //{
        //    var ohlcvList = new List<Ohlcv>();
        //    var apiUrl = $"v1/ohlcv/{_BitcoinOptions.Symbol}/history?period_id={_BitcoinOptions.PeriodId}&limit={_BitcoinOptions.Limit}";
        //    var client2 = new RestClient(_BitcoinOptions.ApiBaseUrl);
        //    var request = new RestRequest(apiUrl, Method.Get);
        //    request.AddHeader("X-CoinAPI-Key", _BitcoinOptions.ApiKey);

        //    try
        //    {
        //        var collection = _ConnectDatabaseServce.ConnectDatabase();

        //        var lastRecord = collection.Find(Builders<BsonDocument>.Filter.Empty)
        //                       .SortByDescending(d => d["time_period_end"])
        //                       .Limit(1)
        //                       .FirstOrDefault();

        //        var lastRecordDate = lastRecord != null ? lastRecord["time_period_end"].ToString() : null;

        //        // Calculate the start date for the API request
        //        var startDate = lastRecordDate != null
        //                        ? DateTime.Parse(lastRecordDate).AddDays(1).ToString("yyyy-MM-ddTHH:mm:ss")
        //                        : DateTime.UtcNow.AddYears(-5).ToString("yyyy-MM-ddTHH:mm:ss");

        //        RestResponse response = client2.Execute(request);

        //        if (response.StatusCode == HttpStatusCode.OK)
        //        {
        //            var content = response.Content;
        //            ohlcvList = JsonConvert.DeserializeObject<List<Ohlcv>>(content);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new Exception(ex.Message, ex);
        //    }

        //    return ohlcvList;
        //}

        //private void _SaveDataInDatabase(List<Ohlcv> ohlcvsList)
        //{
        //    IMongoCollection<BsonDocument> collection = _ConnectDatabaseServce.ConnectDatabase();

        //    foreach (var ohlcv in ohlcvsList)
        //    {
        //        TimeZoneInfo tzBrasil = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        //        DateTime lastUpdate = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tzBrasil);

        //        ohlcv.LastUpdate = lastUpdate;

        //        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("time_period_start", ohlcv.time_period_start);

        //        UpdateOptions updateOptions = new UpdateOptions
        //        {
        //            // Se o documento não existir, ele será inserido
        //            IsUpsert = true
        //        };
        //        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update
        //            .Set("time_period_start", ohlcv.time_period_start)
        //            .Set("time_period_end", ohlcv.time_period_end)
        //            .Set("time_open", ohlcv.time_open)
        //            .Set("time_close", ohlcv.time_close)
        //            .Set("price_open", ohlcv.price_open)
        //            .Set("price_high", ohlcv.price_high)
        //            .Set("price_low", ohlcv.price_low)
        //            .Set("price_close", ohlcv.price_close)
        //            .Set("volume_traded", ohlcv.volume_traded)
        //            .Set("trades_count", ohlcv.trades_count)
        //            .Set("LastUpdate", lastUpdate);

        //        collection.UpdateOne(filter, update, updateOptions);
        //    }

        //    Console.WriteLine("Bitcoin data has been inserted into MongoDB.");
        //}
    }
}
