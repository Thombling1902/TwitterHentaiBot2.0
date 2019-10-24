using System;
using System.Xml;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Autofac;

using Tweetinvi;
using Tweetinvi.Models;
using Tweetinvi.Parameters;
using Tweetinvi.Models.Entities;
using Tweetinvi.Core.Public.Models.Enum;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TwitterHentaiBot2._0
{
    internal class Program
    {
        public static string[] blockedImages = File.ReadAllLines("./blockedimages.txt"); // To block out illegal and the dreaded 404.png. Drop this into your application folder where TwitterHentaiBot2.0.dll is: https://www.dropbox.com/s/jzx9p5hrdnxp5vg/blockedimages.txt?dl=0
        public static string[] endpoints = File.ReadAllLines("./endpoints.txt"); // Download this file and put into the application folder: https://www.dropbox.com/s/5kyvnxx8w8be6n4/endpoints.txt?dl=0
        public static string[] captions = File.ReadAllLines("./captions.txt"); // Do the same for this file: https://www.dropbox.com/s/mv1t85jcklc1kg0/captions.txt?dl=0
        public static string hashtags = "#NSFW #hentai #lewd #cum #neko #tits #pussy #gif #trap #sissy #femboy #porn #anal"; // Change if you want.
        public static string imageBaseDirectory = "./images"; // Feel free to change this to another folder. All this does is specify where to save the images to.
        public static string APIURL = "https://www.nekos.life/api/v2/img/"; // Don't change or things go tits up (No this does not mean you get more porn).
        public static bool saveToDedicatedFolders = true; // Saves images from ie: The anal endpoint to a new folder called "anal". Great for sorting out your wank material later.
        public static Random rng = new Random(); // Random.
        public static WebClient client = new WebClient(); // Webclient to download and query the results.

        public class ReturnInfomation // This is a json class to wrap the json results from nekos.life. Makes it cleaner.
        {
            [JsonProperty("url")]
            public string URL { get; set; }
        }

        private static void Main(string[] args)
        {
            Login(); // Execute the login void.
        }

        private static void Login()
        {
            /* Create a text file called 'credentials.txt' after building the project. Put your application details in as follows:
             * API key on line 1
             * API secret key on line 2
             * Access token on line 3
             * Access token secret on line 4
             */
            string[] credentials = File.ReadAllLines("./credentials.txt");
            Auth.SetCredentials(new TwitterCredentials
            {
                ConsumerKey = credentials[0],
                ConsumerSecret = credentials[1],
                AccessToken = credentials[2],
                AccessTokenSecret = credentials[3]
            });

            IAuthenticatedUser authenticatedUser = User.GetAuthenticatedUser();
            Console.WriteLine($"Authed as: {authenticatedUser.Name}");

            SetupFolders(); // Setting up your folders.
            Poster(); // Executing the recursive function.
        }

        private static void SetupFolders()
        {
            // This is creating your directories if they don't already exist.

            if (!Directory.Exists(imageBaseDirectory))
            {
                Directory.CreateDirectory(imageBaseDirectory);
            }

            if (saveToDedicatedFolders)
            {
                foreach (string endpointFolderToMake in endpoints)
                {
                    if (!Directory.Exists($"{imageBaseDirectory}/{endpointFolderToMake}"))
                    {
                        Directory.CreateDirectory($"{imageBaseDirectory}/{endpointFolderToMake}");
                    }
                }
            }
        }

        private static void Poster()
        {
            string caption = captions[rng.Next(captions.Length)];
            Thread.Sleep(TimeSpan.FromTicks(2)); // This is to reset the Random system. Which runs off system time.
            string endpoint = endpoints[rng.Next(endpoints.Length)];
            Console.WriteLine($"Caption: {caption}\nEndpoint: {endpoint}");

            try // Because shit happens.
            {
                string jsonString = client.DownloadString(APIURL + endpoint); // Downloading JSON.
                client.Dispose(); // Because memory leaks.
                ReturnInfomation returnInfomation = JsonConvert.DeserializeObject<ReturnInfomation>(jsonString); // Parsing JSON data into the ReturnInfomation class earlier.
                string URL = returnInfomation.URL; // Getting the URL.

                string[] URLSplit = URL.Split('/').Last().Split('.'); // Splitting the URL into parts for below.

                // All these are as said.

                string fileName = URLSplit[0];
                string fileExtension = URLSplit[1];
                string fullFileName = $"{fileName}.{fileExtension}";
                byte[] bytesToUpload;

                if (blockedImages.Contains(fullFileName))
                {
                    return;
                }

                if (saveToDedicatedFolders) // If you are saving to dedicated folders. If not change saveToDedicatedFolders to false on line 29. DO NOT DELETE THIS.
                {
                    // This is all just saving the file and then getting the bytes.

                    if (!File.Exists($"{imageBaseDirectory}/{endpoint}/{fullFileName}"))
                    {
                        client.DownloadFile(URL, $"{imageBaseDirectory}/{endpoint}/{fullFileName}");
                        bytesToUpload = File.ReadAllBytes($"{imageBaseDirectory}/{endpoint}/{fullFileName}");
                    }
                    else
                    {
                        bytesToUpload = File.ReadAllBytes($"{imageBaseDirectory}/{endpoint}/{fullFileName}");
                    }
                }
                else
                {
                    if (!File.Exists($"{imageBaseDirectory}/{fullFileName}"))
                    {
                        client.DownloadFile(URL, $"{imageBaseDirectory}/{fullFileName}");
                        bytesToUpload = File.ReadAllBytes($"{imageBaseDirectory}/{fullFileName}");
                    }
                    else
                    {
                        bytesToUpload = File.ReadAllBytes($"{imageBaseDirectory}/{fullFileName}");
                    }
                }

                IMedia mediaToUpload; // Creating the twitter media

                if (fileExtension == "mp4") // Checking if the file is a .mp4
                {
                    mediaToUpload = Upload.UploadBinary(new UploadParameters // Uploading
                    {
                        Binary = bytesToUpload,
                        MediaType = MediaType.VideoMp4
                    });
                }
                else
                {
                    mediaToUpload = Upload.UploadBinary(new UploadParameters // Uploading
                    {
                        Binary = bytesToUpload,
                        MediaType = MediaType.Media
                    });
                }

                List<IMedia> medias = new List<IMedia> { mediaToUpload };

                ITweet tweet = Tweet.PublishTweet($"{caption}\n\n{hashtags}", new PublishTweetOptionalParameters // Publishing... Finally!
                {
                    Medias = medias
                });
            }
            catch (Exception ex) // Shit probably happened.
            {
                Console.WriteLine(ex.ToString());
            }

            Console.WriteLine($"Tweeted. Waiting 5 minutes.");
            Thread.Sleep(TimeSpan.FromMinutes(5)); // Wait a minute to not spam twitters API and get rate limited.
            Poster(); // To make this recursive without threadlocking.
        }
    }
}