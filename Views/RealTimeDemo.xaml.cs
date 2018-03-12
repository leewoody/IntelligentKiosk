﻿// 
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// 
// Microsoft Cognitive Services: http://www.microsoft.com/cognitive
// 
// Microsoft Cognitive Services Github:
// https://github.com/Microsoft/Cognitive
// 
// Copyright (c) Microsoft Corporation
// All rights reserved.
// 
// MIT License:
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// 

using IntelligentKioskSample.Controls;
using Microsoft.ProjectOxford.Common.Contract;
using Microsoft.ProjectOxford.Face.Contract;
using ServiceHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Graphics.Imaging;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

using Plugin.MediaManager;
using Plugin.MediaManager.Abstractions.Enums;
using Plugin.MediaManager.Abstractions.EventArguments;
using Plugin.MediaManager.Abstractions.Implementations;
using Windows.Storage;
// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace IntelligentKioskSample.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    [KioskExperience(Title = "Realtime Crowd Insights", ImagePath = "ms-appx:/Assets/realtime.png", ExperienceType = ExperienceType.Kiosk)]
    public sealed partial class RealTimeDemo : Page, IRealTimeDataProvider
    {
        private Task processingLoopTask;
        private bool isProcessingLoopInProgress;
        private bool isProcessingPhoto;

        private IEnumerable<Face> lastDetectedFaceSample;
        private IEnumerable<Tuple<Face, IdentifiedPerson>> lastIdentifiedPersonSample;
        private IEnumerable<SimilarFaceMatch> lastSimilarPersistedFaceSample;

        private DemographicsData demographics;
        private Dictionary<Guid, Visitor> visitors = new Dictionary<Guid, Visitor>();

        public RealTimeDemo()
        {
            this.InitializeComponent();
            
            this.DataContext = this;

            Window.Current.Activated += CurrentWindowActivationStateChanged;
            this.cameraControl.SetRealTimeDataProvider(this);
            this.cameraControl.FilterOutSmallFaces = false;
            this.cameraControl.HideCameraControls();
            this.cameraControl.CameraAspectRatioChanged += CameraControl_CameraAspectRatioChanged;

            CrossMediaManager.Current.PlayingChanged += OnPlayingChanged;
            CrossMediaManager.Current.BufferingChanged += OnBufferingChanged;
            CrossMediaManager.Current.StatusChanged += OnStatusChanged;
            CrossMediaManager.Current.VideoPlayer.RenderSurface = VideoCanvas;
            CrossMediaManager.Current.MediaFileChanged += CurrentOnMediaFileChanged;

        }
        private void OnBufferingChanged(object sender, BufferingChangedEventArgs bufferingChangedEventArgs)
        {
            var bufferingProgress = bufferingChangedEventArgs?.BufferProgress ?? 0;
            var bufferingTime = bufferingChangedEventArgs?.BufferedTime;
            //Debug.WriteLine($"buffering progress: {bufferingProgress}, buffering time: {bufferingTime}");
        }

        private void CurrentOnMediaFileChanged(object sender, MediaFileChangedEventArgs mediaFileChangedEventArgs)
        {
            var mediaFile = mediaFileChangedEventArgs.File;
            realtimeText.Text = mediaFile.Metadata.Title ?? "";
            //Artist.Text = mediaFile.Metadata.Artist ?? "";
            //Album.Text = mediaFile.Metadata.Album ?? "";
            switch (mediaFile.Type)
            {
                case MediaFileType.Audio:
                    if (mediaFile.Metadata.AlbumArt != null)
                    {
                       // CoverArt.Source = (ImageSource)mediaFile.Metadata.AlbumArt;
                    }
                    break;
                case MediaFileType.Video:
                    break;
            }
        }

        private async void OnStatusChanged(object sender, StatusChangedEventArgs e)
        {
            //await
                //CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                //    () =>
                await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    debugText.Text = Enum.GetName(typeof(MediaPlayerStatus), e.Status);
                        switch (CrossMediaManager.Current.Status)
                        {
                            case MediaPlayerStatus.Stopped:
                                //Progress.Value = 0;
                                break;
                            case MediaPlayerStatus.Paused:
                                break;
                            case MediaPlayerStatus.Playing:
                                //Progress.Maximum = 1;
                                break;
                            case MediaPlayerStatus.Buffering:
                                break;
                            case MediaPlayerStatus.Loading:
                                break;
                            case MediaPlayerStatus.Failed:
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    });
        }

        private async void OnPlayingChanged(object sender, PlayingChangedEventArgs e)
        {
            //await
            //    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            //        () =>
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                        //Progress.Value = e.Progress;
                    });
        }
        private void CameraControl_CameraAspectRatioChanged(object sender, EventArgs e)
        {
            this.UpdateCameraHostSize();
        }

        private void StartProcessingLoop()
        {
            this.isProcessingLoopInProgress = true;

            if (this.processingLoopTask == null || this.processingLoopTask.Status != TaskStatus.Running)
            {
                this.processingLoopTask = Task.Run(() => this.ProcessingLoop());
            }
        }


        private async void ProcessingLoop()
        {
            while (this.isProcessingLoopInProgress)
            {
                await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    if (!this.isProcessingPhoto)
                    {
                        if (DateTime.Now.Day != this.demographics.StartTime.Day)//////
                        {
                            // We have been running through the day. Reset the data...
                            await this.ResetDemographicsData();
                            this.UpdateDemographicsUI();
                        }

                        this.isProcessingPhoto = true;
                        if (this.cameraControl.NumFacesOnLastFrame == 0)
                        {
                            await this.ProcessCameraCapture(null);
                        }
                        else
                        {
                            await this.ProcessCameraCapture(await this.cameraControl.CaptureFrameAsync());
                        }
                    }
                });

                await Task.Delay(1000);
            }
        }

        private async void CurrentWindowActivationStateChanged(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
            if ((e.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.CodeActivated ||
                e.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.PointerActivated) &&
                this.cameraControl.CameraStreamState == Windows.Media.Devices.CameraStreamState.Shutdown)
            {
                // When our Window loses focus due to user interaction Windows shuts it down, so we 
                // detect here when the window regains focus and trigger a restart of the camera.
                await this.cameraControl.StartStreamAsync(isForRealTimeProcessing: true);
            }
        }

        private async Task ProcessCameraCapture(ImageAnalyzer e)
        {
            if (e == null)
            {
                this.lastDetectedFaceSample = null;
                this.lastIdentifiedPersonSample = null;
                this.lastSimilarPersistedFaceSample = null;
                this.debugText.Text = "";

                this.isProcessingPhoto = false;
                return;
            }

            DateTime start = DateTime.Now;

            // Compute Emotion, Age and Gender
            await this.DetectFaceAttributesAsync(e);

            // Compute Face Identification and Unique Face Ids
            await Task.WhenAll(ComputeFaceIdentificationAsync(e), this.ComputeUniqueFaceIdAsync(e));

            this.UpdateDemographics(e);
            this.UpdateEmotionTimelineUI(e);

            this.debugText.Text = string.Format("Latency: {0}ms", (int)(DateTime.Now - start).TotalMilliseconds);

            this.isProcessingPhoto = false;
        }

        private async Task ComputeUniqueFaceIdAsync(ImageAnalyzer e)
        {
            await e.FindSimilarPersistedFacesAsync();

            if (!e.SimilarFaceMatches.Any())
            {
                this.lastSimilarPersistedFaceSample = null;
            }
            else
            {
                this.lastSimilarPersistedFaceSample = e.SimilarFaceMatches;
            }
        }

        private async Task ComputeFaceIdentificationAsync(ImageAnalyzer e)
        {
            await e.IdentifyFacesAsync();

            if (!e.IdentifiedPersons.Any())
            {
                this.lastIdentifiedPersonSample = null;
            }
            else
            {
                this.lastIdentifiedPersonSample = e.DetectedFaces.Select(f => new Tuple<Face, IdentifiedPerson>(f, e.IdentifiedPersons.FirstOrDefault(p => p.FaceId == f.FaceId)));
            }
        }

        private async Task DetectFaceAttributesAsync(ImageAnalyzer e)
        {
            await e.DetectFacesAsync(detectFaceAttributes: true);

            if (e.DetectedFaces == null || !e.DetectedFaces.Any())
            {
                this.lastDetectedFaceSample = null;
            }
            else
            {
                this.lastDetectedFaceSample = e.DetectedFaces;
            }
        }

        private void UpdateEmotionTimelineUI(ImageAnalyzer e)
        {
            if (!e.DetectedFaces.Any())
            {
                this.ShowTimelineFeedbackForNoFaces();
            }
            else
            {
                EmotionScores averageScores = new EmotionScores
                {
                    Happiness = e.DetectedFaces.Average(f => f.FaceAttributes.Emotion.Happiness),
                    Anger = e.DetectedFaces.Average(f => f.FaceAttributes.Emotion.Anger),
                    Sadness = e.DetectedFaces.Average(f => f.FaceAttributes.Emotion.Sadness),
                    Contempt = e.DetectedFaces.Average(f => f.FaceAttributes.Emotion.Contempt),
                    Disgust = e.DetectedFaces.Average(f => f.FaceAttributes.Emotion.Disgust),
                    Neutral = e.DetectedFaces.Average(f => f.FaceAttributes.Emotion.Neutral),
                    Fear = e.DetectedFaces.Average(f => f.FaceAttributes.Emotion.Fear),
                    Surprise = e.DetectedFaces.Average(f => f.FaceAttributes.Emotion.Surprise)
                };

                this.emotionDataTimelineControl.DrawEmotionData(averageScores);
            }
        }

        private void ShowTimelineFeedbackForNoFaces()
        {
            this.emotionDataTimelineControl.DrawEmotionData(new EmotionScores { Neutral = 1 });
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            EnterKioskMode();

            if (string.IsNullOrEmpty(SettingsHelper.Instance.FaceApiKey))
            {
                await new MessageDialog("Missing Face API Key. Please enter a key in the Settings page.", "Missing API Key").ShowAsync();
            }
            else
            {
                FaceListManager.FaceListsUserDataFilter = SettingsHelper.Instance.FaceApiKey + "_RealTime";//////
                await FaceListManager.Initialize();

                await ResetDemographicsData();
                this.UpdateDemographicsUI();

                await this.cameraControl.StartStreamAsync(isForRealTimeProcessing: true);
                this.StartProcessingLoop();
            }

            base.OnNavigatedTo(e);
        }
        private MediaFile mediaFile;
        private async void UpdateDemographics(ImageAnalyzer img)
        {
            if (this.lastSimilarPersistedFaceSample != null)
            {
                bool demographicsChanged = false;
                mediaFile = new MediaFile(@"http://clips.vorwaerts-gmbh.de/big_buck_bunny.mp4", MediaFileType.Video);
                // Update the Visitor collection (either add new entry or update existing)
                foreach (var item in this.lastSimilarPersistedFaceSample)
                {
                    Visitor visitor;
                    String unique = "1";
                    if (this.visitors.TryGetValue(item.SimilarPersistedFace.PersistedFaceId, out visitor))
                    {
                        visitor.Count++;
                        unique = "0";
                    }
                    else
                    {
                        demographicsChanged = true;

                        visitor = new Visitor { UniqueId = item.SimilarPersistedFace.PersistedFaceId, Count = 1 };
                        this.visitors.Add(visitor.UniqueId, visitor);
                        this.demographics.Visitors.Add(visitor);

                        // Update the demographics stats. We only do it for new visitors to avoid double counting. 
                        AgeDistribution genderBasedAgeDistribution = null;
                        if (string.Compare(item.Face.FaceAttributes.Gender, "male", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            this.demographics.OverallMaleCount++;
                            genderBasedAgeDistribution = this.demographics.AgeGenderDistribution.MaleDistribution;
                        }
                        else
                        {
                            this.demographics.OverallFemaleCount++;
                            genderBasedAgeDistribution = this.demographics.AgeGenderDistribution.FemaleDistribution;
                        }

                        if (item.Face.FaceAttributes.Age < 16)
                        {
                            genderBasedAgeDistribution.Age0To15++;
                        }
                        else if (item.Face.FaceAttributes.Age < 20)
                        {
                            genderBasedAgeDistribution.Age16To19++;
                        }
                        else if (item.Face.FaceAttributes.Age < 30)
                        {
                            genderBasedAgeDistribution.Age20s++;
                        }
                        else if (item.Face.FaceAttributes.Age < 40)
                        {
                            genderBasedAgeDistribution.Age30s++;
                        }
                        else if (item.Face.FaceAttributes.Age < 50)
                        {
                            genderBasedAgeDistribution.Age40s++;
                        }
                        else
                        {
                            genderBasedAgeDistribution.Age50sAndOlder++;
                        }
                    }
                    if (lastDetectedFaceSample != null)
                    {
                        Random rand = new Random();
                        Dictionary<String, String> dictionary = new Dictionary<String, String>();

                        dictionary["id"] = item.SimilarPersistedFace.PersistedFaceId.ToString();
                        dictionary["gender"] = item.Face.FaceAttributes.Gender.ToString();
                        dictionary["age"] = item.Face.FaceAttributes.Age.ToString();
                        dictionary["date"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                        dictionary["smile"] = item.Face.FaceAttributes.Smile.ToString();
                        dictionary["glasses"] = item.Face.FaceAttributes.Glasses.ToString();
                        dictionary["avgs"] = rand.Next(5, 8).ToString();
                        dictionary["avgrank"] = (3 + rand.NextDouble() * 1.5).ToString();

                        EmotionScores averageScores = new EmotionScores
                        {
                            Happiness = img.DetectedFaces.Average(f => f.FaceAttributes.Emotion.Happiness),
                            Anger = img.DetectedFaces.Average(f => f.FaceAttributes.Emotion.Anger),
                            Sadness = img.DetectedFaces.Average(f => f.FaceAttributes.Emotion.Sadness),
                            Contempt = img.DetectedFaces.Average(f => f.FaceAttributes.Emotion.Contempt),
                            Disgust = img.DetectedFaces.Average(f => f.FaceAttributes.Emotion.Disgust),
                            Neutral = img.DetectedFaces.Average(f => f.FaceAttributes.Emotion.Neutral),
                            Fear = img.DetectedFaces.Average(f => f.FaceAttributes.Emotion.Fear),
                            Surprise = img.DetectedFaces.Average(f => f.FaceAttributes.Emotion.Surprise)
                        };

                        dictionary["isunique"] = unique;
                        dictionary["anger"] = averageScores.Anger.ToString();
                        dictionary["contempt"] = averageScores.Contempt.ToString();
                        dictionary["disgust"] = averageScores.Disgust.ToString();
                        dictionary["fear"] = averageScores.Fear.ToString();
                        dictionary["happiness"] = averageScores.Happiness.ToString();
                        dictionary["neutral"] = averageScores.Neutral.ToString();
                        dictionary["sadness"] = averageScores.Sadness.ToString();
                        dictionary["surprise"] = averageScores.Surprise.ToString();

                        //#pragma warning restore 4014
                        System.Diagnostics.Debug.WriteLine("here!!!!!!!!");
                        var name = "null";
                        var person = "";
                        System.Diagnostics.Debug.WriteLine("Identify? : " + lastIdentifiedPersonSample == null);
                        if (null != lastIdentifiedPersonSample && null != lastIdentifiedPersonSample.First().Item2)
                        {
                            name = lastIdentifiedPersonSample.First().Item2.Person.Name.ToString();
                            person = lastIdentifiedPersonSample.First().Item2.Person.PersonId.ToString();
                            //realtimeTitleControl.Text = name;
                            //辨識到後
                            //await CrossMediaManager.Current.Stop();
                            //await CrossMediaManager.Current.Play(mediaFile);
                        }

                        System.Diagnostics.Debug.WriteLine("Name: " + name);
                        System.Diagnostics.Debug.WriteLine("ID: " + person);
                        foreach (KeyValuePair<string, string> entry in dictionary)
                        {
                            System.Diagnostics.Debug.WriteLine(entry.Key.ToString() + ": " + entry.Value.ToString());
                            // do something with entry.Value or entry.Key
                        }
                        dictionary["personid"] = person;
                        dictionary["personname"] = name;
                        //realtimeTitleControl.Text = name;
                        ////#pragma warning disable 4014
                        String str = SettingsHelper.Instance.IoTHubConnectString;
                        await IoTClient.Start(dictionary, SettingsHelper.Instance.IoTHubConnectString);

                    }
                }

                if (demographicsChanged)
                {
                    //realtimeText.Text = "來訪人次:" + this.demographics.Visitors.Count + "\n 政府福利政策";
                    if(this.demographics.OverallMaleCount > this.demographics.OverallFemaleCount)
                    {
                        realtimeText.Text = "來訪人次:" + this.demographics.Visitors.Count + "\n男性政府福利政策";
                        //mediaFile = new MediaFile(@"http://shuj.shu.edu.tw/2016/wp-content/uploads/2016/10/2178%E6%9C%9F-%E3%80%8A%E9%AB%98%E9%BD%A1%E5%8C%96%E7%A4%BE%E6%9C%83%E4%BE%86%E8%87%A8-%E5%AE%89%E9%A4%8A%E6%A9%9F%E6%A7%8B%E4%BA%BA%E5%8A%9B%E4%B8%8D%E8%B6%B3%E5%87%A1%E6%B7%B3%E6%AF%93%E7%8F%8A%E3%80%8BB6.mp4?_=1", MediaFileType.Video);
                        //mediaFile = new MediaFile(@"http://clips.vorwaerts-gmbh.de/big_buck_bunny.mp4", MediaFileType.Video);
                        mediaFile = new MediaFile(@"ms-appx:///Assets/B6.mp4", MediaFileType.Video);
                        await CrossMediaManager.Current.Stop();
                        //await CrossMediaManager.Current.Play(mediaFile);
                        var file = await KnownFolders.VideosLibrary.GetFileAsync("B6.mp4");
                        await CrossMediaManager.Current.Play(file.Path, MediaFileType.Video, ResourceAvailability.Local);

                    }
                    else
                    {

                        realtimeText.Text = "來訪人次:" + this.demographics.Visitors.Count + "\n 女性政府福利政策";
                        mediaFile = new MediaFile(@"http://shuj.shu.edu.tw/2016/wp-content/uploads/2016/10/2178%E6%9C%9F-%E3%80%8A%E9%AB%98%E9%BD%A1%E5%8C%96%E7%A4%BE%E6%9C%83%E4%BE%86%E8%87%A8-%E5%AE%89%E9%A4%8A%E6%A9%9F%E6%A7%8B%E4%BA%BA%E5%8A%9B%E4%B8%8D%E8%B6%B3%E5%87%A1%E6%B7%B3%E6%AF%93%E7%8F%8A%E3%80%8BB6.mp4?_=1", MediaFileType.Video);
                        await CrossMediaManager.Current.Stop();
                        await CrossMediaManager.Current.Play(mediaFile);

                    }


                    this.ageGenderDistributionControl.UpdateData(this.demographics);
                }

                this.overallStatsControl.UpdateData(this.demographics);
            }
        }

        private void UpdateDemographicsUI()
        {
            this.ageGenderDistributionControl.UpdateData(this.demographics);
            this.overallStatsControl.UpdateData(this.demographics);
        }

        private async Task ResetDemographicsData()
        {
            this.initializingUI.Visibility = Visibility.Visible;
            this.initializingProgressRing.IsActive = true;

            this.demographics = new DemographicsData
            {
                StartTime = DateTime.Now,
                AgeGenderDistribution = new AgeGenderDistribution { FemaleDistribution = new AgeDistribution(), MaleDistribution = new AgeDistribution() },
                Visitors = new List<Visitor>()
            };

            this.visitors.Clear();
            await FaceListManager.ResetFaceLists();

            this.initializingUI.Visibility = Visibility.Collapsed;
            this.initializingProgressRing.IsActive = false;
        }

        public async Task HandleApplicationShutdownAsync()
        {
            await ResetDemographicsData();
        }

        private void EnterKioskMode()
        {
            ApplicationView view = ApplicationView.GetForCurrentView();
            if (!view.IsFullScreenMode)
            {
                view.TryEnterFullScreenMode();
            }
        }

        protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            this.isProcessingLoopInProgress = false;
            Window.Current.Activated -= CurrentWindowActivationStateChanged;
            this.cameraControl.CameraAspectRatioChanged -= CameraControl_CameraAspectRatioChanged;

            await this.ResetDemographicsData();

            await this.cameraControl.StopStreamAsync();
            base.OnNavigatingFrom(e);
        }

        private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.UpdateCameraHostSize();
        }

        private void UpdateCameraHostSize()
        {
            this.cameraHostGrid.Width = this.cameraHostGrid.ActualHeight * (this.cameraControl.CameraAspectRatio != 0 ? this.cameraControl.CameraAspectRatio : 1.777777777777);
        }

        public Face GetLastFaceAttributesForFace(BitmapBounds faceBox)
        {
            if (this.lastDetectedFaceSample == null || !this.lastDetectedFaceSample.Any())
            {
                return null;
            }

            return Util.FindFaceClosestToRegion(this.lastDetectedFaceSample, faceBox);
        }

        public IdentifiedPerson GetLastIdentifiedPersonForFace(BitmapBounds faceBox)
        {
            if (this.lastIdentifiedPersonSample == null || !this.lastIdentifiedPersonSample.Any())
            {
                return null;
            }

            Tuple<Face, IdentifiedPerson> match =
                this.lastIdentifiedPersonSample.Where(f => Util.AreFacesPotentiallyTheSame(faceBox, f.Item1.FaceRectangle))
                                               .OrderBy(f => Math.Abs(faceBox.X - f.Item1.FaceRectangle.Left) + Math.Abs(faceBox.Y - f.Item1.FaceRectangle.Top)).FirstOrDefault();
            if (match != null)
            {
                if (null != match.Item2)
                {
                    realtimeTitleControl.Text = "歡迎VIP:" + match.Item2.Person.Name.ToString() +", 蒞臨指導";
                     
                }
                return match.Item2;
            }

            return null;
        }

        public SimilarPersistedFace GetLastSimilarPersistedFaceForFace(BitmapBounds faceBox)
        {
            if (this.lastSimilarPersistedFaceSample == null || !this.lastSimilarPersistedFaceSample.Any())
            {
                return null;
            }

            SimilarFaceMatch match =
                this.lastSimilarPersistedFaceSample.Where(f => Util.AreFacesPotentiallyTheSame(faceBox, f.Face.FaceRectangle))
                                               .OrderBy(f => Math.Abs(faceBox.X - f.Face.FaceRectangle.Left) + Math.Abs(faceBox.Y - f.Face.FaceRectangle.Top)).FirstOrDefault();

            return match?.SimilarPersistedFace;
        }

        private void TextBlock_SelectionChanged(object sender, RoutedEventArgs e)
        {

        }
    }

    [XmlType]
    public class Visitor
    {
        [XmlAttribute]
        public Guid UniqueId { get; set; }

        [XmlAttribute]
        public int Count { get; set; }
    }

    [XmlType]
    public class AgeDistribution
    {
        public int Age0To15 { get; set; }
        public int Age16To19 { get; set; }
        public int Age20s { get; set; }
        public int Age30s { get; set; }
        public int Age40s { get; set; }
        public int Age50sAndOlder { get; set; }
    }

    [XmlType]
    public class AgeGenderDistribution
    {
        public AgeDistribution MaleDistribution { get; set; }
        public AgeDistribution FemaleDistribution { get; set; }
    }

    [XmlType]
    [XmlRoot]
    public class DemographicsData
    {
        public DateTime StartTime { get; set; }

        public AgeGenderDistribution AgeGenderDistribution { get; set; }

        public int OverallMaleCount { get; set; }

        public int OverallFemaleCount { get; set; }

        [XmlArrayItem]
        public List<Visitor> Visitors { get; set; }
    }
}