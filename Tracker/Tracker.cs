namespace Tracker
{

    using TrackerUtils;


    using Microsoft.Azure.CognitiveServices.Language.TextAnalytics;
    using Microsoft.Azure.CognitiveServices.Language.TextAnalytics.Models;
    using Microsoft.CognitiveServices.SpeechRecognition;

    using System;
    using System.Linq;
    using System.Configuration;
    using System.Windows.Forms;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Windows.Threading;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using System.Reflection;

    public partial class Tracker : Form
    {

        private HookingManager _hookMgr;
        // client for text analysis
        ITextAnalyticsAPI client = new TextAnalyticsAPI();
        // The data recognition client
        //private DataRecognitionClient dataClient;
        // The microphone client
        private MicrophoneRecognitionClient micClient;
        Dispatcher dispUI;


        Assembly _assembly = Assembly.GetExecutingAssembly();

        Image moodPic1;
        Image moodPic2;
        Image moodPic3;
        Image moodPic4;
        Image moodPic5;

        private Dictionary<long, Tuple<string, double>> sentenceHistory = new Dictionary<long, Tuple<string, double>>();

        List<Point> graphPoints = new List<Point>();
        Pen graphPen = new Pen(Color.Blue, 2);

        public Tracker()
        {
            InitializeComponent();

            this.Size = new Size(560, 255);

            var procs = ConfigurationManager.AppSettings["processesToMonitor"].Split(',').ToList();
            procs.ForEach(x => x.Trim());
            this._hookMgr = new HookingManager(procs);
            this._hookMgr.SendSentenceUp += _hookMgr_SendSentenceUp;

            // configure teh client....
            client.AzureRegion = AzureRegions.Westus;
            client.SubscriptionKey = "1fbb6b04cf42479181278c93fb2e45df";

            moodPic1 = Image.FromFile("images/emotion0.png", true);
            moodPic2 = Image.FromFile("images/emotion1.png", true);
            moodPic3 = Image.FromFile("images/emotion2.png", true);
            moodPic4 = Image.FromFile("images/emotion3.png", true);
            moodPic5 = Image.FromFile("images/emotion4.png", true);

        }

        /// <summary>
        /// Gets the LUIS application identifier.
        /// </summary>
        /// <value>
        /// The LUIS application identifier.
        /// </value>
        private string LuisAppId
        {
            get { return ConfigurationManager.AppSettings["luisAppID"]; }
        }

        /// <summary>
        /// Gets the LUIS subscription identifier.
        /// </summary>
        /// <value>
        /// The LUIS subscription identifier.
        /// </value>
        private string LuisSubscriptionID
        {
            get { return ConfigurationManager.AppSettings["luisSubscriptionID"]; }
        }

        /// <summary>
        /// when starting, register hook to capture keyboard input
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnStartTracking_Click(object sender, EventArgs e)
        {

            dispUI = Dispatcher.CurrentDispatcher;
            this._hookMgr.Hook();
            this.btnStartTracking.Enabled = false;
            this.btnStopTracking.Enabled = true;

        }

        private void btnStopTracking_Click(object sender, EventArgs e)
        {
            this._hookMgr.Unhook();
            this.btnStartTracking.Enabled = true;
            this.btnStopTracking.Enabled = false;
        }

        private void _hookMgr_SendSentenceUp(string message)
        {
            ParseSentenceForSentiment(message);
        }

        private void ParseSentenceForSentiment(string message)
        {
            // call API for analysis
            SentimentBatchResult result = client.Sentiment(
               new MultiLanguageBatchInput(
                   new List<MultiLanguageInput>()
                   {
                          new MultiLanguageInput("en", "0", message)
                   }));


            Task.Factory.StartNew
                       (() => dispUI.BeginInvoke
                                   (new Action
                                        (() => this.lblParsedSentence.Text = message), null));

            var _score = result.Documents.First().Score;

            this.UpdateUI(() => this.lblScore.Text = _score.Value.ToString("0.00"));

            ProcessResult(message, result, _score);
        }

        private void ProcessResult(string message, SentimentBatchResult result, double? _score)
        {
            if (result.Documents.First().Score.HasValue)
                this.sentenceHistory.Add(DateTime.UtcNow.Ticks,
                    new Tuple<string, double>(message, _score.Value));

            // calculate average feeling
            var average = this.sentenceHistory.Average(x => x.Value.Item2);

            this.UpdateUI(() => this.lblAverageScore.Text = average.ToString("0.00"));
            this.UpdateUI(() => SetMoodIcon(average));

            var _score2 = Convert.ToInt32(_score.Value * 5);
            graphPoints.Add(
                new Point((5 * (graphPoints.Count + 1)),
                this.Panel.Height / (_score2 == 0 ? 1 : _score2)));

            Debug.WriteLine($"{_score.Value} {_score.Value * 5} - ");
            Debug.WriteLine($"Last Point X:{graphPoints.Last().X}, Y:{graphPoints.Last().Y}");

            UpdateUI(() =>
           {
               Graphics g = this.Panel.CreateGraphics();
               g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
               Pen pen = new Pen(Color.Blue, 1);
               if (graphPoints.Count() == 1)
                   g.DrawLine(pen, new Point(0, 0 + this.Panel.Height), graphPoints.Last());
               else
                   g.DrawLine(pen, graphPoints.Skip(graphPoints.Count() - 2).Take(1).First(), graphPoints.Last());
           });


        }

        private void UpdateUI(Action uiAction) => Task.Factory.StartNew(() => dispUI.BeginInvoke(uiAction));


        private void SetMoodIcon(double average)
        {
            if (average < .2d)
                this.moodPicture.Image = moodPic1;
            if (average >= .2d && average < .4d)
                this.moodPicture.Image = moodPic2;
            if (average >= .4d && average < .6d)
                this.moodPicture.Image = moodPic3;
            if (average >= .6d && average < .8d)
                this.moodPicture.Image = moodPic4;
            if (average >= .8d)
                this.moodPicture.Image = moodPic5;
        }

        private void btnRecordJournal_Click(object sender, EventArgs e)
        {
            dispUI = Dispatcher.CurrentDispatcher;

            this.btnRecordJournal.Enabled = false;
            this.CreateMicrophoneRecoClient();
            this.micClient.StartMicAndRecognition();
            this.txtLog.Text = string.Empty;
            this.Size = new Size(this.Size.Width, this.Size.Height + 250);
            this.txtLog.Text = "Starting...";
            this.txtLog.Text += Environment.NewLine;
        }



        private void CreateMicrophoneRecoClient()
        {
            this.micClient = SpeechRecognitionServiceFactory.CreateMicrophoneClient(
                SpeechRecognitionMode.LongDictation,
                "en-US",            // assume this default locale
                ConfigurationManager.AppSettings["luisSubscriptionID"]);

            //this.micClient.AuthenticationUri = this.AuthenticationUri;

            // Event handlers for speech recognition results
            this.micClient.OnMicrophoneStatus += this.OnMicrophoneStatus;
            this.micClient.OnResponseReceived += this.OnMicDictationResponseReceivedHandler;

            this.micClient.OnConversationError += this.OnConversationErrorHandler;
        }

        /// <summary>
        /// Called when a final response is received;
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SpeechResponseEventArgs"/> instance containing the event data.</param>
        private void OnMicDictationResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            //this.WriteLine("--- OnMicDictationResponseReceivedHandler ---");
            if (e.PhraseResponse.RecognitionStatus == RecognitionStatus.EndOfDictation ||
                e.PhraseResponse.RecognitionStatus == RecognitionStatus.DictationEndSilenceTimeout)
            {
                // we got the final result, so it we can end the mic reco.  No need to do this
                // for dataReco, since we already called endAudio() on it as soon as we were done
                // sending all the data.
                this.micClient.EndMicAndRecognition();

                UpdateUI(() => this.btnRecordJournal.Enabled = true);
                UpdateUI(() => this.Size = new Size(this.Size.Width, this.Size.Height - 250));

            }

            this.WriteResponseResult(e);
        }

        private void WriteLine()
        {
            this.WriteLine(string.Empty);
        }

        private void WriteLine(string format, params object[] args)
        {
            var formattedStr = string.Format(format, args);
            var t = Task.Factory.StartNew
                       (() => dispUI.BeginInvoke
                                   (new Action
                                        (() => this.txtLog.Text += formattedStr + "\n"), null));
        }

        private void WriteResponseResult(SpeechResponseEventArgs e)
        {
            if (e.PhraseResponse.Results.Length == 0)
            {
                this.WriteLine("No phrase response is available.");
                UpdateUI(() => this.btnRecordJournal.Enabled = true);
            }
            else
            {
                for (int i = 0; i < e.PhraseResponse.Results.Length; i++)
                {
                    this.WriteLine($"[{i}] - Confidence: {e.PhraseResponse.Results[i].Confidence} Text:{e.PhraseResponse.Results[i].DisplayText}{Environment.NewLine}");
                }

                // select one with highest confidence
                var _text = e.PhraseResponse.Results.OrderByDescending(x => x.Confidence).First().DisplayText;
                // parse this sentnce
                ParseSentenceForSentiment(_text);

                this.WriteLine();

                UpdateUI(() => this.txtLog.ScrollToCaret());
            }
        }

        private void OnMicrophoneStatus(object sender, MicrophoneEventArgs e)
        {
            if (e.Recording)
                WriteLine("Please start speaking." + Environment.NewLine);
            else
                WriteLine("Mic not ready or not working." + Environment.NewLine);

        }

        /// <summary>
        /// Called when an error is received.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SpeechErrorEventArgs"/> instance containing the event data.</param>
        private void OnConversationErrorHandler(object sender, SpeechErrorEventArgs e)
        {
            this.WriteLine("Error:");
            this.WriteLine("Error code: {0}", e.SpeechErrorCode.ToString());
            this.WriteLine("Error text: {0}", e.SpeechErrorText);
        }


        private void Tracker_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (null != this.micClient)
                this.micClient.Dispose();
            this._hookMgr.Unhook();
            base.OnClosed(e);

        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            this.lblAverageScore.Text = "";
            this.moodPicture.Image = null;
            this.lblParsedSentence.Text = "";
            this.lblScore.Text = "";
            this.graphPoints.Clear();
            this.sentenceHistory.Clear();
            this.Panel.Invalidate();
            this.txtLog.Text = "";
        }
    }
}
