using Microsoft.ProjectOxford.Vision;
using Microsoft.ProjectOxford.Vision.Contract;
using Plugin.Media;
using Plugin.Media.Abstractions;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;
using Plugin.TextToSpeech;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace XamerinFinal
{
	public partial class MainPage : ContentPage
	{
        const string APIKEY = "7a7bccecb620420089ae8e1ed3d7104f";
        const string APIROOT = "https://westcentralus.api.cognitive.microsoft.com/vision/v1.0";

        public MainPage()
		{
			InitializeComponent();
		}

        private async Task<AnalysisResult> GetImageDescription(Stream imageStream)
        {
            // Get a connection to the Vision API
            VisionServiceClient visionClient = new VisionServiceClient(APIKEY, APIROOT);
            // Select the features that we will use
            List<VisualFeature> features = new List<VisualFeature> { VisualFeature.Tags, VisualFeature.Description };

            // Analyze the image
            return await visionClient.AnalyzeImageAsync(imageStream, features, null);
        }

        private void ProcessImageResults(AnalysisResult result)
        {
            // Check if there's an image before doing anything
            if (theImage.Source == null)
            {
                theResults.Text = "Please select an image before processing!";
                return;
            }

            try
            {
                // Clear the results Label
                theResults.Text = "";

                if (result.Description.Captions[0].ToString().Length > 0)
                {
                    // If we have a description to work with
                    
                    // Clear the results Label
                    theResults.Text = "";

                    // Check if the API Confidence is a float
                    bool success = float.TryParse(result.Description.Captions[0].Confidence.ToString(), out float value);
                    // Ceil the confidence and convert to int
                    int confidence = Convert.ToInt32(value * 100.0f + 0.5f);
                    // Display the Description and Confidence
                    theResults.Text += result.Description.Captions[0].Text + Environment.NewLine + "(Confidence: " + confidence.ToString() + "% )" + Environment.NewLine;
                }

                // Loop through each tag
                foreach (var tag in result.Tags)
                {
                    // Append the tag name to the results Label with a , at the end
                    theResults.Text += tag.Name + ", ";
                }

                if (theResults.Text.Length > 2)
                {
                    // If we have basically any data (a tag would be at least 2) in the results Label
                    // Remove the last 2 characters (which should be a , )
                    theResults.Text = theResults.Text.Remove(theResults.Text.Length - 2);
                }
                else
                {
                    // If we get here then theres really just no data to work with
                    theResults.Text = "Nothing could be recognized";
                }
            }
            catch (ClientException ex)
            {
                theResults.Text = "No results: " + ex;
            }
            finally
            {
                // Let the user know that the apps done processing
                theActivityIndicator.IsRunning = false;
                Speak();
            }
        }

        private async void GetImageDataFromFile(MediaFile file)
        {
            // Let the user know the app is loading something
            theActivityIndicator.IsRunning = true;

            try
            {
                // Wait for the Vision API to return the tags and description of the image
                var result = await GetImageDescription(file.GetStream());

                // Handle the results
                ProcessImageResults(result);
            }
            catch (ClientException ex)
            {
                theResults.Text = ex.Message;
            }

            // Apps done loading stuff, let the user know
            theActivityIndicator.IsRunning = false;
        }

        private async void Speak()
        {
            // Give the CrossTextToSpeech plugin some text to read
            await CrossTextToSpeech.Current.Speak(theResults.Text);
        }

        private async void WebImageButton_Clicked(object sender, EventArgs e)
        {
            Uri webImageUri = new Uri("https://placeimg.com/640/480");

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    using (var reponse = await client.GetStreamAsync(webImageUri))
                    {
                        theActivityIndicator.IsRunning = true;

                        var memoryStream = new MemoryStream();
                        await reponse.CopyToAsync(memoryStream);
                        memoryStream.Position = 0;

                        try
                        {
                            var result = await GetImageDescription(memoryStream);
                            memoryStream.Position = 0;
                            theImage.Source = ImageSource.FromStream(() => memoryStream);
                            ProcessImageResults(result);
                        }
                        catch (Exception ex)
                        {
                            theResults.Text = "Failed to load the image: " + ex.Message;
                        }

                        theActivityIndicator.IsRunning = false;
                    }
                }
            }
            catch(Exception ex)
            {
                theResults.Text = "Failed to load the image: " + ex.Message;
            }
        }

        private async void ImageButton_Clicked(object sender, EventArgs e)
        {
            // Wait for the file selector to load
            await CrossMedia.Current.Initialize();

            // if files can be loaded then continue
            if (CrossMedia.Current.IsPickPhotoSupported)
            {
                // Select the file
                var file = await CrossMedia.Current.PickPhotoAsync();
                // Check if the fil exists
                if(file == null)
                {
                    return;
                }

                // If we are here then the file exists
                // Set the image container source to the file we selected
                theImage.Source = ImageSource.FromStream(() => { return file.GetStream(); });

                // Read the image
                GetImageDataFromFile(file);
            }
        }

        private async void CameraButton_Clicked(object sender, EventArgs e)
        {
            // Get the permission status for camera and storage
            var cameraStatus = await CrossPermissions.Current.CheckPermissionStatusAsync(Permission.Camera);
            var storageStatus = await CrossPermissions.Current.CheckPermissionStatusAsync(Permission.Storage);

            // If we don't have permission, then request it
            if (cameraStatus != PermissionStatus.Granted || storageStatus != PermissionStatus.Granted)
            {
                var results = await CrossPermissions.Current.RequestPermissionsAsync(new[] { Permission.Camera, Permission.Storage });
                cameraStatus = results[Permission.Camera];
                storageStatus = results[Permission.Storage];
            }
            
            // If we have permission, then we can continue capturing an image
            if (cameraStatus == PermissionStatus.Granted && storageStatus == PermissionStatus.Granted)
            {
                // Capture an image (Sample/test.jpg)
                var file = await CrossMedia.Current.TakePhotoAsync(new StoreCameraMediaOptions
                {
                    DefaultCamera = CameraDevice.Front,
                    Directory = "Sample",
                    Name = "test.jpg",
                    SaveToAlbum = true
                });

                // Place the image inside the image container
                theImage.Source = ImageSource.FromStream(() =>
                {
                    var stream = file.GetStream();
                    file.Dispose();
                    return stream;
                });

                // Read the image
                GetImageDataFromFile(file);
            }
            else
            {
                // Permission Denied
                await DisplayAlert("Permissions Denied", "Unable to take photos.", "OK");
                // Take the user to the (ios) app settings to let them fix the problem
                CrossPermissions.Current.OpenAppSettings();
            }

        }
    }
}
