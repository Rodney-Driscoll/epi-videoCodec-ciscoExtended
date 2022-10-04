﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using PepperDash.Core;
using PepperDash.Core.Intersystem.Tokens;
using PepperDash.Core.Intersystem;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.DeviceInfo;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Core.Routing;
using PepperDash.Essentials.Devices.Common.Cameras;
using PepperDash.Essentials.Devices.Common.Codec;
using PepperDash.Essentials.Devices.Common.VideoCodec;
using PepperDash.Essentials.Core.Queues;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace epi_videoCodec_ciscoExtended
{
    internal enum eCommandType
    {
        SessionStart,
        SessionEnd,
        Command,
        GetStatus,
        GetConfiguration
    };

    public enum eExternalSourceType
    {
        camera,
        desktop,
        document_camera,
        mediaplayer,
        PC,
        whiteboard,
        other
    }

    public enum eExternalSourceMode
    {
        Ready,
        NotReady,
        Hidden,
        Error
    }

    public enum eCodecPresentationStates
    {
        LocalOnly,
        LocalRemote
    }

    public enum eCameraTrackingCapabilities
    {
        None,
        SpeakerTrack,
        PresenterTrack,
        Both
    }



    public class CiscoCodec : VideoCodecBase, IHasCallHistory, IHasCallFavorites, IHasDirectory,
        IHasScheduleAwareness, IOccupancyStatusProvider, IHasCodecLayoutsAvailable, IHasCodecSelfView,
        ICommunicationMonitor, IRouting, IHasCodecCameras, IHasCameraAutoMode, IHasCodecRoomPresets,
        IHasExternalSourceSwitching, IHasBranding, IHasCameraOff, IHasCameraMute, IHasDoNotDisturbMode,
        IHasHalfWakeMode, IHasCallHold, IJoinCalls, IDeviceInfoProvider
    {



        public event EventHandler<AvailableLayoutsChangedEventArgs> AvailableLayoutsChanged;
        public event EventHandler<CurrentLayoutChangedEventArgs> CurrentLayoutChanged;
        private event EventHandler<MinuteChangedEventArgs> MinuteChanged;
        public event EventHandler<CodecInfoChangedEventArgs> CodecInfoChanged;
        public event EventHandler<CameraTrackingCapabilitiesArgs> CameraTrackingCapabilitiesChanged;

        public eCameraTrackingCapabilities CameraTrackingCapabilities { get; private set; }

        private CTimer _scheduleCheckTimer;
        private DateTime _scheduleCheckLast;

        public Meeting ActiveMeeting { get; private set; }

        private const int XSigEncoding = 28591;

        private List<Meeting> _currentMeetings; 

        private readonly Version _testedCodecFirmware = new Version("10.11.5.2");
        private readonly Version _enhancedLayoutsFirmware = new Version("9.15.10.8");
        public Version CodecFirmware { get; private set; }

        private bool EnhancedLayouts
        {
            get
            {
                if (CodecFirmware == null) return false;
                var returnValue = CodecFirmware.CompareTo(_enhancedLayoutsFirmware) >= 0;
                Debug.Console(0, this, "Enhanced Layout Functionality is {0}.", returnValue ? "enabled" : "disabled");
                return (returnValue);
            }
        }


        private Meeting _currentMeeting;

        private bool _presentationActive;

        private readonly bool _phonebookAutoPopulate;
        private bool _phonebookInitialSearch;

        private string _lastSearched;
        private CiscoCodecConfig _config;
        private readonly int _joinableCooldownSeconds;

        public eCodecPresentationStates PresentationStates;

        private bool _externalSourceChangeRequested;

        public bool Room { get; private set; }

        public event EventHandler<DirectoryEventArgs> DirectoryResultReturned;

        private CTimer _brandingTimer;

        public CommunicationGather PortGather { get; private set; }

        public StatusMonitorBase CommunicationMonitor { get; private set; }

        private readonly GenericQueue _receiveQueue;

        public BoolFeedback PresentationViewMaximizedFeedback { get; private set; }

        public BoolFeedback PresentationViewMinimizedFeedback { get; private set; }

        public BoolFeedback PresentationViewDefaultFeedback { get; private set; }

        public FeedbackGroup PresentationViewFeedbackGroup { get; private set; }

        private string _currentPresentationView;

        private eCameraTrackingCapabilities PreferredTrackingMode { get; set; }

        public BoolFeedback RoomIsOccupiedFeedback { get; private set; }

        public IntFeedback PeopleCountFeedback { get; private set; }

        public BoolFeedback SelfviewIsOnFeedback { get; private set; }

        public StringFeedback SelfviewPipPositionFeedback { get; private set; }

        public StringFeedback LocalLayoutFeedback { get; private set; }

        public BoolFeedback PresentationActiveFeedback { get; private set; }

        private string _currentLayoutBacker;

        private string CurrentLayout
        {
            get
            {
                return _currentLayoutBacker != "Grid" ? _currentLayoutBacker : "Side by Side";
            }
            set { _currentLayoutBacker = value; }
        }

        public StringFeedback AvailableLayoutsFeedback { get; private set; }

        public BoolFeedback LocalLayoutIsProminentFeedback { get; private set; }

        public BoolFeedback FarEndIsSharingContentFeedback { get; private set; }

        #region AutoCamera Feedbacks

        public BoolFeedback CameraAutoModeIsOnFeedback { get; private set; }
        public BoolFeedback SpeakerTrackStatusOnFeedback { get; private set; }
        public BoolFeedback PresenterTrackStatusOnFeedback { get; private set; }

        public StringFeedback PresenterTrackStatusNameFeedback { get; private set; }
        public BoolFeedback PresenterTrackStatusOffFeedback { get; private set; }
        public BoolFeedback PresenterTrackStatusFollowFeedback { get; private set; }
        public BoolFeedback PresenterTrackStatusBackgroundFeedback { get; private set; }
        public BoolFeedback PresenterTrackStatusPersistentFeedback { get; private set; }

        public BoolFeedback CameraAutoModeAvailableFeedback { get; private set; }
        public BoolFeedback SpeakerTrackAvailableFeedback { get; private set; }
        public BoolFeedback PresenterTrackAvailableFeedback { get; private set; }

        public FeedbackGroup PresenterTrackFeedbackGroup { get; private set; }

        #endregion

        public IntFeedback RingtoneVolumeFeedback { get; private set; }

        public List<CodecCommandWithLabel> AvailableLayouts { get; private set; }

        private CodecCommandWithLabel _currentSelfviewPipPosition;

        private readonly List<CodecCommandWithLabel> _legacyLayouts = new List<CodecCommandWithLabel>()
        {
            //new CodecCommandWithLabel("auto", "Auto"),
            //new CiscoCodecLocalLayout("custom", "Custom"),    // Left out for now
            new CodecCommandWithLabel("equal", "Equal"),
            new CodecCommandWithLabel("overlay", "Overlay"),
            new CodecCommandWithLabel("prominent", "Prominent"),
            new CodecCommandWithLabel("single", "Single")
        };

        private CodecCommandWithLabel _currentLegacyLayout; 


        /// <summary>
        /// List the available positions for the selfview PIP window
        /// </summary>
        public List<CodecCommandWithLabel> SelfviewPipPositions = new List<CodecCommandWithLabel>()
        {
            new CodecCommandWithLabel("CenterLeft", "Center Left"),
            new CodecCommandWithLabel("CenterRight", "Center Right"),
            new CodecCommandWithLabel("LowerLeft", "Lower Left"),
            new CodecCommandWithLabel("LowerRight", "Lower Right"),
            new CodecCommandWithLabel("UpperCenter", "Upper Center"),
            new CodecCommandWithLabel("UpperLeft", "Upper Left"),
            new CodecCommandWithLabel("UpperRight", "Upper Right"),
        };

        private CiscoCodecConfiguration.RootObject CodecConfiguration = new CiscoCodecConfiguration.RootObject();

        private CiscoCodecStatus.RootObject CodecStatus = new CiscoCodecStatus.RootObject();

        public CodecCallHistory CallHistory { get; private set; }

        public CodecCallFavorites CallFavorites { get; private set; }

        /// <summary>
        /// The root level of the directory
        /// </summary>
        public CodecDirectory DirectoryRoot { get; private set; }

        /// <summary>
        /// Represents the current state of the directory and is computed on get
        /// </summary>
        public CodecDirectory CurrentDirectoryResult
        {
            get
            {
                if (DirectoryBrowseHistory.Count > 0)
                    return DirectoryBrowseHistory[DirectoryBrowseHistory.Count - 1];
                else
                    return DirectoryRoot;
            }
        }

        public BoolFeedback CurrentDirectoryResultIsNotDirectoryRoot { get; private set; }

        /// <summary>
        /// Tracks the directory browse history when browsing beyond the root directory
        /// </summary>
        public List<CodecDirectory> DirectoryBrowseHistory { get; private set; }

        public CodecScheduleAwareness CodecSchedule { get; private set; }

        /// <summary>
        /// Gets and returns the scaled volume of the codec
        /// </summary>
        protected override Func<int> VolumeLevelFeedbackFunc
        {
            get
            {
                return
                    () =>
                        CrestronEnvironment.ScaleWithLimits(CodecStatus.Status.Audio.Volume.IntValue, 100, 0, 65535, 0);
            }
        }

        protected override Func<bool> PrivacyModeIsOnFeedbackFunc
        {
            get { return () => CodecStatus.Status.Audio.Microphones.Mute.BoolValue; }
        }

        protected override Func<bool> StandbyIsOnFeedbackFunc
        {
            get { return () => CodecStatus.Status.Standby.State.BoolValue; }
        }

        /// <summary>
        /// Gets the value of the currently shared source, or returns null
        /// </summary>
        protected override Func<string> SharingSourceFeedbackFunc
        {
            get { return () => _presentationSourceKey; }
        }

        protected override Func<bool> SharingContentIsOnFeedbackFunc
        {
            get { return () => CodecStatus.Status.StatusConference.Presentation.ModeValueProperty.BoolValue; }
        }

        protected Func<bool> FarEndIsSharingContentFeedbackFunc
        {
            get { return () => CodecStatus.Status.StatusConference.Presentation.ModeValueProperty.Value == "Receiving"; }
        }

        protected override Func<bool> MuteFeedbackFunc
        {
            get { return () => CodecStatus.Status.Audio.VolumeMute.BoolValue; }
        }

        protected Func<bool> RoomIsOccupiedFeedbackFunc
        {
            get { return () => CodecStatus.Status.RoomAnalytics.PeoplePresence.BoolValue; }
        }

        protected Func<bool> PresentationActiveFeedbackFunc
        {
            get { return () => CodecStatus.Status.StatusConference.Presentation.ModeValueProperty.Value != "Off"; }
        }

        protected Func<int> PeopleCountFeedbackFunc
        {
            get { return () => CodecStatus.Status.RoomAnalytics.PeopleCount.CurrentPeopleCount.IntValue; }
        }


        protected Func<string> AvailableLayoutsFeedbackFunc
        {
            get { return () => UpdateLayoutsXSig(AvailableLayouts); }
        }

        protected Func<bool> SelfViewIsOnFeedbackFunc
        {
            get { return () => CodecStatus.Status.Video.Selfview.SelfViewMode.BoolValue; }
        }

        protected Func<string> SelfviewPipPositionFeedbackFunc
        {
            get { return () => _currentSelfviewPipPosition.Label; }
        }

        protected Func<string> LocalLayoutFeedbackFunc
        {
            get { return () => CurrentLayout; }
        }

        protected Func<bool> LocalLayoutIsProminentFeedbackFunc
        {
            get { return () => CurrentLayout.Contains("Prominent") || CurrentLayout.Contains("Stack"); }
        }

        #region CameraAutoTrackingFeedbackFunc


        protected Func<bool> CameraTrackingAvailableFeedbackFunc
        {
            get
            {
                return () => CodecStatus.Status.Cameras.PresenterTrack.Availability.BoolValue
                             || CodecStatus.Status.Cameras.SpeakerTrack.Availability.BoolValue;
            }
        }

        protected Func<bool> CameraTrackingOnFeedbackFunc
        {
            get
            {
                return () => (CodecStatus.Status.Cameras.SpeakerTrack.Availability.BoolValue
                             && CodecStatus.Status.Cameras.SpeakerTrack.SpeakerTrackStatus.BoolValue)
                             || (CodecStatus.Status.Cameras.PresenterTrack.Availability.BoolValue
                             && CodecStatus.Status.Cameras.PresenterTrack.PresenterTrackStatus.BoolValue);

            }
        }

        protected Func<bool> PresenterTrackAvailableFeedbackFunc
        {
            get { return () => CodecStatus.Status.Cameras.PresenterTrack.Availability.BoolValue; }
        }

        protected Func<bool> SpeakerTrackAvailableFeedbackFunc
        {
            get { return () => CodecStatus.Status.Cameras.SpeakerTrack.Availability.BoolValue; }
        }

        protected Func<bool> SpeakerTrackStatusOnFeedbackFunc
        {
            get { return () => CodecStatus.Status.Cameras.SpeakerTrack.SpeakerTrackStatus.BoolValue; }
        }

        protected Func<string> PresenterTrackStatusNameFeedbackFunc
        {
            get { return () => CodecStatus.Status.Cameras.PresenterTrack.PresenterTrackStatus.StringValue; }
        }

        protected Func<bool> PresenterTrackStatusOnFeedbackFunc
        {
            get
            {
                return () => ((CodecStatus.Status.Cameras.PresenterTrack.PresenterTrackStatus.BoolValue)
                              || (String.IsNullOrEmpty(CodecStatus.Status.Cameras.PresenterTrack.PresenterTrackStatus.StringValue)));
            }
        }


        protected Func<bool> PresenterTrackStatusOffFeedbackFunc
        {
            get { return () => CodecStatus.Status.Cameras.PresenterTrack.PresenterTrackStatus.StringValue.ToLower() == "off"; }
        }

        protected Func<bool> PresenterTrackStatusFollowFeedbackFunc
        {
            get { return () => CodecStatus.Status.Cameras.PresenterTrack.PresenterTrackStatus.Value.ToLower() == "follow"; }
        }

        protected Func<bool> PresenterTrackStatusBackgroundFeedbackFunc
        {
            get { return () => CodecStatus.Status.Cameras.PresenterTrack.PresenterTrackStatus.Value.ToLower() == "background"; }
        }

        protected Func<bool> PresenterTrackStatusPersistentFeedbackFunc
        {
            get { return () => CodecStatus.Status.Cameras.PresenterTrack.PresenterTrackStatus.Value.ToLower() == "persistent"; }
        }

        #endregion





        private CodecSyncState _syncState;

        public CodecPhonebookSyncState PhonebookSyncState { get; private set; }

        private StringBuilder _jsonMessage;

        private bool _jsonFeedbackMessageIsIncoming;

        public bool CommDebuggingIsOn;

        private string Delimiter = "\r\n";

        public IntFeedback PresentationSourceFeedback { get; private set; }

        public BoolFeedback PresentationSendingLocalOnlyFeedback { get; private set; }

        public BoolFeedback PresentationSendingLocalRemoteFeedback { get; private set; }

        /// <summary>
        /// Used to track the current connector used for the presentation source
        /// </summary>
        private int _presentationSource;

        /// <summary>
        /// Used to track the connector that is desired to be the current presentation source (until the command is send)
        /// </summary>
        private int _desiredPresentationSource;

        private string _presentationSourceKey;

        private bool _presentationLocalOnly;

        private bool _presentationLocalRemote;

        private string _phonebookMode = "Local"; // Default to Local

        private uint _phonebookResultsLimit = 255; // Could be set later by config.

        private CTimer _loginMessageReceivedTimer;
        private CTimer _retryConnectionTimer;

        // **___________________________________________________________________**
        //  Timers to be moved to the global system timer at a later point....
        private CTimer BookingsRefreshTimer;
        private CTimer PhonebookRefreshTimer;
        // **___________________________________________________________________**

        public RoutingInputPort CodecOsdIn { get; private set; }
        public RoutingInputPort HdmiIn2 { get; private set; }
        public RoutingInputPort HdmiIn3 { get; private set; }
        public RoutingOutputPort HdmiOut1 { get; private set; }
        public RoutingOutputPort HdmiOut2 { get; private set; }


        // Constructor for IBasicCommunication
        public CiscoCodec(DeviceConfig config, IBasicCommunication comm)
            : base(config)
        {
            DeviceInfo = new DeviceInfo();
            CodecInfo = new CiscoCodecInfo(this);
            _lastSearched = string.Empty;
            _phonebookInitialSearch = true;
            CurrentLayout = string.Empty;
            PresentationStates = eCodecPresentationStates.LocalOnly;


            var props = JsonConvert.DeserializeObject<CiscoCodecConfig>(config.Properties.ToString());

            _config = props;

            MeetingsToDisplay = _config.OverrideMeetingsLimit ? 50 : 0;
            _timeFormatSpecifier = _config.TimeFormatSpecifier.NullIfEmpty() ?? "t";
            _dateFormatSpecifier = _config.DateFormatSpecifier.NullIfEmpty() ?? "d";
            _joinableCooldownSeconds = _config.JoinableCooldownSeconds;

            PreferredTrackingMode = eCameraTrackingCapabilities.SpeakerTrack;

            var trackingMode = _config.DefaultCameraTrackingMode.ToLower();


            if (!String.IsNullOrEmpty(trackingMode))
            {
                if (trackingMode.Contains("presenter"))
                {
                    PreferredTrackingMode = eCameraTrackingCapabilities.PresenterTrack;
                }
            }

            // Use the configured phonebook results limit if present
            if (props.PhonebookResultsLimit > 0)
            {
                _phonebookResultsLimit = props.PhonebookResultsLimit;
            }

            // The queue that will collect the repsonses in the order they are received
            _receiveQueue = new GenericQueue(this.Key + "-rxQueue", 25);

            RoomIsOccupiedFeedback = new BoolFeedback(RoomIsOccupiedFeedbackFunc);
            PeopleCountFeedback = new IntFeedback(PeopleCountFeedbackFunc);
            SelfviewIsOnFeedback = new BoolFeedback(SelfViewIsOnFeedbackFunc);
            SelfviewPipPositionFeedback = new StringFeedback(SelfviewPipPositionFeedbackFunc);
            LocalLayoutFeedback = new StringFeedback(LocalLayoutFeedbackFunc);
            LocalLayoutIsProminentFeedback = new BoolFeedback(LocalLayoutIsProminentFeedbackFunc);
            FarEndIsSharingContentFeedback = new BoolFeedback(FarEndIsSharingContentFeedbackFunc);
            CameraIsOffFeedback = new BoolFeedback(() => CodecStatus.Status.Video.VideoInput.MainVideoMute.BoolValue);
            AvailableLayoutsFeedback = new StringFeedback(AvailableLayoutsFeedbackFunc);

            PresentationActiveFeedback = new BoolFeedback(PresentationActiveFeedbackFunc);


            #region CameraAutoFeedbackRegistration

            CameraAutoModeIsOnFeedback = new BoolFeedback(CameraTrackingOnFeedbackFunc);
            SpeakerTrackStatusOnFeedback = new BoolFeedback(SpeakerTrackStatusOnFeedbackFunc);
            PresenterTrackStatusOnFeedback = new BoolFeedback(PresenterTrackStatusOnFeedbackFunc);

            PresenterTrackStatusNameFeedback = new StringFeedback(PresenterTrackStatusNameFeedbackFunc);
            PresenterTrackStatusOffFeedback = new BoolFeedback(PresenterTrackStatusOffFeedbackFunc);
            PresenterTrackStatusFollowFeedback = new BoolFeedback(PresenterTrackStatusFollowFeedbackFunc);
            PresenterTrackStatusBackgroundFeedback = new BoolFeedback(PresenterTrackStatusBackgroundFeedbackFunc);
            PresenterTrackStatusPersistentFeedback = new BoolFeedback(PresenterTrackStatusPersistentFeedbackFunc);

            CameraAutoModeAvailableFeedback = new BoolFeedback(CameraTrackingAvailableFeedbackFunc);
            PresenterTrackAvailableFeedback = new BoolFeedback(PresenterTrackAvailableFeedbackFunc);
            SpeakerTrackAvailableFeedback = new BoolFeedback(SpeakerTrackAvailableFeedbackFunc);

            PresenterTrackFeedbackGroup = new FeedbackGroup(new FeedbackCollection<Feedback>()
            {
                PresenterTrackStatusOnFeedback,
                PresenterTrackStatusNameFeedback,
                PresenterTrackStatusOffFeedback,
                PresenterTrackStatusFollowFeedback,
                PresenterTrackStatusBackgroundFeedback,
                PresenterTrackStatusPersistentFeedback,
            });

            #endregion



            CameraIsMutedFeedback = CameraIsOffFeedback;
            SupportsCameraOff = true;

            DoNotDisturbModeIsOnFeedback = new BoolFeedback(() => CodecStatus.Status.StatusConference.DoNotDisturb.BoolValue);
            HalfWakeModeIsOnFeedback =
                new BoolFeedback(() => CodecStatus.Status.Standby.State.Value.ToLower() == "halfwake");
            EnteringStandbyModeFeedback =
                new BoolFeedback(() => CodecStatus.Status.Standby.State.Value.ToLower() == "enteringstandby");

            PresentationViewMaximizedFeedback = new BoolFeedback(() => _currentPresentationView == "Maximized");
            PresentationViewMinimizedFeedback = new BoolFeedback(() => _currentPresentationView == "Minimized");
            PresentationViewDefaultFeedback = new BoolFeedback(() => _currentPresentationView == "Default");

            PresentationViewFeedbackGroup = new FeedbackGroup(new FeedbackCollection<Feedback>()
            {
                PresentationViewMaximizedFeedback,
                PresentationViewMinimizedFeedback,
                PresentationViewDefaultFeedback
            });

            RingtoneVolumeFeedback =
                new IntFeedback(() => CodecConfiguration.Configuration.Audio.SoundsAndAlerts.RingVolume.Volume);

            PresentationSourceFeedback = new IntFeedback(() => _presentationSource);
            PresentationSendingLocalOnlyFeedback = new BoolFeedback(() => _presentationLocalOnly);
            PresentationSendingLocalRemoteFeedback = new BoolFeedback(() => _presentationLocalRemote);

            Communication = comm;

            if (props.CommunicationMonitorProperties != null)
            {
                CommunicationMonitor = new GenericCommunicationMonitor(this, Communication,
                    props.CommunicationMonitorProperties);
            }
            else
            {
                var command = string.Format("xStatus SystemUnit Software DisplayName\r");
                CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 30000, 120000, 300000,
                    command);
            }

            if (props.Sharing != null)
                AutoShareContentWhileInCall = props.Sharing.AutoShareContentWhileInCall;

            ShowSelfViewByDefault = props.ShowSelfViewByDefault;

            DeviceManager.AddDevice(CommunicationMonitor);

            _phonebookMode = props.PhonebookMode;
            _phonebookAutoPopulate = !props.PhonebookDisableAutoPopulate;

            _syncState = new CodecSyncState(Key + "--Sync", this);

            PhonebookSyncState = new CodecPhonebookSyncState(Key + "--PhonebookSync");

            _syncState.InitialSyncCompleted += SyncState_InitialSyncCompleted;

            PortGather = new CommunicationGather(Communication, Delimiter) {IncludeDelimiter = true};
            PortGather.LineReceived += this.Port_LineReceived;


            CallHistory = new CodecCallHistory();



            if (props.Favorites != null)
            {
                CallFavorites = new CodecCallFavorites();
                CallFavorites.Favorites = props.Favorites;
            }

            DirectoryRoot = new CodecDirectory();

            DirectoryBrowseHistory = new List<CodecDirectory>();

            CurrentDirectoryResultIsNotDirectoryRoot = new BoolFeedback(() => DirectoryBrowseHistory.Count > 0);

            CurrentDirectoryResultIsNotDirectoryRoot.FireUpdate();

            CodecSchedule = new CodecScheduleAwareness();

            //Set Feedback Actions
            SetFeedbackActions();

            CodecOsdIn = new RoutingInputPort(RoutingPortNames.CodecOsd,
                eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, new Action(StopSharing), this);
            HdmiIn2 = new RoutingInputPort(RoutingPortNames.HdmiIn2, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, new Action(SelectPresentationSource1), this);
            HdmiIn3 = new RoutingInputPort(RoutingPortNames.HdmiIn3, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, new Action(SelectPresentationSource2), this);
            HdmiOut1 = new RoutingOutputPort(RoutingPortNames.HdmiOut1,
                eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, null, this);
            HdmiOut2 = new RoutingOutputPort(RoutingPortNames.HdmiOut2,
                eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, null, this);

            InputPorts.Add(CodecOsdIn);
            InputPorts.Add(HdmiIn2);
            InputPorts.Add(HdmiIn3);
            OutputPorts.Add(HdmiOut1);
            CreateOsdSource();

            ExternalSourceListEnabled = props.ExternalSourceListEnabled;
            ExternalSourceInputPort = props.ExternalSourceInputPort;
            RegisterSystemUnitEvents();
            RegisterVideoEvents();

            if (props.UiBranding == null)
            {
                return;
            }
            Debug.Console(2, this, "Setting branding properties enable: {0} _brandingUrl {1}", props.UiBranding.Enable,
                props.UiBranding.BrandingUrl);

            BrandingEnabled = props.UiBranding.Enable;

            _brandingUrl = props.UiBranding.BrandingUrl;

            AvailableLayoutsChanged += CiscoCodec_AvailableLayoutsChanged;
            CurrentLayoutChanged += CiscoCodec_CurrentLayoutChanged;
            CallStatusChange += CiscoCodec_CallStatusChange;
            CodecInfoChanged += CiscoCodec_CodecInfoChanged;

        }
        
        void CiscoCodec_CodecInfoChanged(object sender, CodecInfoChangedEventArgs args)
        {
            Debug.Console(0, "CODECINFOCHANGED!!!!");
            if (args.InfoChangeType == eCodecInfoChangeType.Firmware)
            {
                Debug.Console(0, this, "Got Firmware Event!!!!!!");
                if (String.IsNullOrEmpty(args.Firmware)) return;
                CodecFirmware = GetVersion(args.Firmware);
                if (_testedCodecFirmware > CodecFirmware)
                {
                    Debug.Console(0, this,
                        "Be advised that all functionality may not be available for this plugin.\n" +
                        "The installed firmware is {0} and the minimum tested firmware is {1}",
                        CodecFirmware.ToString(), _testedCodecFirmware.ToString());
                }
                if (!String.IsNullOrEmpty(CodecFirmware.ToString()))
                {
                    DeviceInfo.FirmwareVersion = CodecFirmware.ToString();
                    UpdateDeviceInfo();
                }
                _syncState.InitialSoftwareVersionMessageReceived();
            }
            if (args.InfoChangeType == eCodecInfoChangeType.SerialNumber)
            {
                Debug.Console(0, this, "Got Serial Event!!!!!!");

                if (!String.IsNullOrEmpty(args.SerialNumber))
                {
                    DeviceInfo.SerialNumber = args.SerialNumber;
                    UpdateDeviceInfo();
                }
            }
            if (args.InfoChangeType == eCodecInfoChangeType.Network)
            {
                Debug.Console(0, this, "Got Network Event!!!!!!");

                if (!String.IsNullOrEmpty(args.IpAddress))
                {
                    DeviceInfo.IpAddress = args.IpAddress;
                }
            } 
        }

        private void ScheduleTimeCheck(object time)
        {
            DateTime currentTime;

            if (time != null)
            {
                var currentTimeString = (time as string);
                if (String.IsNullOrEmpty(currentTimeString)) return;
                currentTime = DateTime.ParseExact(currentTimeString, "o", CultureInfo.InvariantCulture);
            }
            else currentTime = DateTime.Now;

            if (_scheduleCheckLast == DateTime.MinValue)
            {
                _scheduleCheckLast = currentTime;
                return;
            }
            if (currentTime.Minute == _scheduleCheckLast.Minute) return;
            _scheduleCheckLast = currentTime;
            OnMinuteChanged(currentTime);
        }



        private void OnMinuteChanged(DateTime currentTime)
        {
            var handler = MinuteChanged;
            if (MinuteChanged == null) return;
            handler(this, new MinuteChangedEventArgs(currentTime));
        }

        private eCameraTrackingCapabilities SetDefaultTracking(string data)
        {
            try
            {
                var trackingMode = !data.ToLower().Contains("track")
                    ? data.ToLower() + "track"
                    : data.ToLower();
                return
                    (eCameraTrackingCapabilities) Enum.Parse(typeof (eCameraTrackingCapabilities), trackingMode, true);

            }
            catch (Exception)
            {
                Debug.Console(0, this, "Unable to parse DefaultCameraTrackingMode - SpeakerTrack Set");
                return eCameraTrackingCapabilities.SpeakerTrack;
            }

        }

        private void CiscoCodec_CallStatusChange(object sender, CodecCallStatusItemChangeEventArgs e)
        {
            var callPresent = ActiveCalls.Any(call => call.IsActiveCall);
            if(!EnhancedLayouts) OnAvailableLayoutsChanged(_legacyLayouts);
            if (callPresent) return;
            OnAvailableLayoutsChanged(new List<CodecCommandWithLabel>());
            OnCurrentLayoutChanged(String.Empty);
        }

        private string UpdateActiveMeetingXSig(Meeting currentMeeting)
        {
            //const int _meetingsToDisplay = 3;            
            const int maxDigitals = 3;
            const int maxStrings = 8;
            const int offset = maxDigitals + maxStrings;
            const int digitalIndex = maxStrings; //15
            const int stringIndex = 0;
            const int meetingIndex = 0;
            var meeting = currentMeeting;
            var tokenArray = new XSigToken[offset];
            /* 
             * Digitals
             * IsJoinable - 1
             * IsDialable - 2
             * IsAvailable - 3
             * 
             * Serials
             * Organizer - 1
             * Title - 2
             * Start Date - 3
             * Start Time - 4
             * End Date - 5
             * End Time - 6
             * OrganizerId - 7
             * Active "StartTime - EndTime" - 8
             * TimeRemaining Text - 9
             * TimeRemaining MinutesRemaining Text - 10
             * TimeRemaining MinutesRemaining Analog - 11
             * 
            */
            if (meeting != null)
            {

                //digitals
                tokenArray[digitalIndex] = new XSigDigitalToken(digitalIndex + 1, meeting.Joinable);
                tokenArray[digitalIndex + 1] = new XSigDigitalToken(digitalIndex + 2, meeting.Id != "0");
                tokenArray[digitalIndex + 2] = new XSigDigitalToken(digitalIndex + 3, true);

                //serials
                tokenArray[stringIndex] = new XSigSerialToken(stringIndex + 1, meeting.Organizer);
                tokenArray[stringIndex + 1] = new XSigSerialToken(stringIndex + 2, meeting.Title);
                tokenArray[stringIndex + 2] = new XSigSerialToken(stringIndex + 3,
                    meeting.StartTime.ToString(_dateFormatSpecifier, Global.Culture));
                tokenArray[stringIndex + 3] = new XSigSerialToken(stringIndex + 4,
                    meeting.StartTime.ToString(_timeFormatSpecifier, Global.Culture));
                tokenArray[stringIndex + 4] = new XSigSerialToken(stringIndex + 5,
                    meeting.EndTime.ToString(_dateFormatSpecifier, Global.Culture));
                tokenArray[stringIndex + 5] = new XSigSerialToken(stringIndex + 6,
                    meeting.EndTime.ToString(_timeFormatSpecifier, Global.Culture));
                tokenArray[stringIndex + 6] = new XSigSerialToken(stringIndex + 7, meeting.Id);
                tokenArray[stringIndex + 7] = new XSigSerialToken(stringIndex + 8, String.Format("{0} - {1}",
                    meeting.StartTime.ToString(_timeFormatSpecifier, Global.Culture),
                    meeting.EndTime.ToString(_timeFormatSpecifier, Global.Culture)));

            }

            else
            {
                Debug.Console(2, this, "Clearing unused data. Meeting Index: {0} MaxMeetings * Offset: {1}",
                    meetingIndex, offset);

                //digitals
                tokenArray[digitalIndex] = new XSigDigitalToken(digitalIndex + 1, false);
                tokenArray[digitalIndex + 1] = new XSigDigitalToken(digitalIndex + 2, false);
                tokenArray[digitalIndex + 2] = new XSigDigitalToken(digitalIndex + 3, false);


                //serials
                tokenArray[stringIndex] = new XSigSerialToken(stringIndex + 1, String.Empty);
                tokenArray[stringIndex + 1] = new XSigSerialToken(stringIndex + 2, String.Empty);
                tokenArray[stringIndex + 2] = new XSigSerialToken(stringIndex + 3, String.Empty);
                tokenArray[stringIndex + 3] = new XSigSerialToken(stringIndex + 4, String.Empty);
                tokenArray[stringIndex + 4] = new XSigSerialToken(stringIndex + 5, String.Empty);
                tokenArray[stringIndex + 5] = new XSigSerialToken(stringIndex + 6, String.Empty);
                tokenArray[stringIndex + 6] = new XSigSerialToken(stringIndex + 7, String.Empty);
                tokenArray[stringIndex + 7] = new XSigSerialToken(stringIndex + 8, String.Empty);
            }

            return GetXSigString(tokenArray);
        }


        private void SetFeedbackActions()
        {
            CodecStatus.Status.Audio.Volume.ValueChangedAction = VolumeLevelFeedback.FireUpdate;
            CodecStatus.Status.Audio.VolumeMute.ValueChangedAction = MuteFeedback.FireUpdate;

            CodecStatus.Status.Audio.Microphones.Mute.ValueChangedAction = PrivacyModeIsOnFeedback.FireUpdate;

            CodecStatus.Status.Standby.State.ValueChangedAction = () =>
            {
                StandbyIsOnFeedback.FireUpdate();
                HalfWakeModeIsOnFeedback.FireUpdate();
                EnteringStandbyModeFeedback.FireUpdate();
            };

            CodecStatus.Status.RoomAnalytics.PeoplePresence.ValueChangedAction = RoomIsOccupiedFeedback.FireUpdate;

            CodecStatus.Status.RoomAnalytics.PeopleCount.CurrentPeopleCount.ValueChangedAction = PeopleCountFeedback.FireUpdate;
            
            CodecStatus.Status.Video.Layout.CurrentLayouts.ActiveLayout.ValueChangedAction = () =>
            {
                Debug.Console(0, this, "CurrentLayout = \"{0}\"", CurrentLayout);
                OnCurrentLayoutChanged(CodecStatus.Status.Video.Layout.CurrentLayouts.ActiveLayout.Value);
            };   

            CodecStatus.Status.Video.Selfview.SelfViewMode.ValueChangedAction = SelfviewIsOnFeedback.FireUpdate;

            CodecStatus.Status.Video.Selfview.PipPosition.ValueChangedAction = ComputeSelfviewPipStatus;

            CodecStatus.Status.Video.Layout.CurrentLayouts.ActiveLayout.ValueChangedAction =
                LocalLayoutFeedback.FireUpdate;

            CodecStatus.Status.Video.Layout.LayoutFamily.Local.ValueChangedAction = ComputeLegacyLayout;


            //CodecStatus.PresenterTrackStatus.ChannelVideo.Layout.LayoutFamily.Local.ValueChangedAction = ComputeLocalLayout;
            CodecStatus.Status.StatusConference.Presentation.ModeValueProperty.ValueChangedAction = () =>
            {
                SharingContentIsOnFeedback.FireUpdate();
                FarEndIsSharingContentFeedback.FireUpdate();
                PresentationActiveFeedback.FireUpdate();
            };

            CodecStatus.Status.StatusConference.DoNotDisturb.ValueChangedAction = DoNotDisturbModeIsOnFeedback.FireUpdate;


            CodecConfiguration.Configuration.Audio.SoundsAndAlerts.RingVolume.ValueChangedAction =
                RingtoneVolumeFeedback.FireUpdate;

            #region CameraTrackingFeedbackRegistration

            CodecStatus.Status.Cameras.SpeakerTrack.SpeakerTrackStatus.ValueChangedAction +=
                () =>
                {
                    SpeakerTrackStatusOnFeedback.FireUpdate();
                    CameraAutoModeIsOnFeedback.FireUpdate();
                };
            CodecStatus.Status.Cameras.PresenterTrack.PresenterTrackStatus.ValueChangedAction +=
                () =>
                {
                    PresenterTrackFeedbackGroup.FireUpdate();
                    CameraAutoModeIsOnFeedback.FireUpdate();
                };
            CodecStatus.Status.Cameras.SpeakerTrack.Availability.ValueChangedAction +=
                () =>
                {
                    SpeakerTrackAvailableFeedback.FireUpdate();
                    CameraAutoModeAvailableFeedback.FireUpdate();
                    OnCameraTrackingCapabilitiesChanged();
                };
            CodecStatus.Status.Cameras.PresenterTrack.Availability.ValueChangedAction +=
                () =>
                {
                    PresenterTrackAvailableFeedback.FireUpdate();
                    CameraAutoModeAvailableFeedback.FireUpdate();
                    OnCameraTrackingCapabilitiesChanged();
                };

            #endregion

            try
            {
                CodecStatus.Status.Video.VideoInput.MainVideoMute.ValueChangedAction = CameraIsOffFeedback.FireUpdate;
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "Error setting MainVideoMute Action: {0}", ex);

                if (ex.InnerException != null)
                {
                    Debug.Console(0, this, "Error setting MainVideoMute Action: {0}", ex);
                }
            }
        }

        /// <summary>
        /// Creates the fake OSD source, and connects it's AudioVideo output to the CodecOsdIn input
        /// to enable routing 
        /// </summary>
        private void CreateOsdSource()
        {
            OsdSource = new DummyRoutingInputsDevice(Key + "[osd]");
            DeviceManager.AddDevice(OsdSource);
            var tl = new TieLine(OsdSource.AudioVideoOutputPort, CodecOsdIn);
            TieLineCollection.Default.Add(tl);
        }

        public void InitializeBranding(string roomKey)
        {
            Debug.Console(1, this, "Initializing Branding for room {0}", roomKey);

            if (!BrandingEnabled)
            {
                return;
            }

            var mcBridgeKey = String.Format("mobileControlBridge-{0}", roomKey);

            var mcBridge = DeviceManager.GetDeviceForKey(mcBridgeKey) as IMobileControlRoomBridge;

            if (!String.IsNullOrEmpty(_brandingUrl))
            {
                Debug.Console(1, this, "Branding URL found: {0}", _brandingUrl);
                if (_brandingTimer != null)
                {
                    _brandingTimer.Stop();
                    _brandingTimer.Dispose();
                }

                _brandingTimer = new CTimer((o) =>
                {
                    if (_sendMcUrl)
                    {
                        SendMcBrandingUrl(mcBridge);
                        _sendMcUrl = false;
                    }
                    else
                    {
                        SendBrandingUrl();
                        _sendMcUrl = true;
                    }
                }, 0, 15000);
            }
            else if (String.IsNullOrEmpty(_brandingUrl))
            {
                Debug.Console(1, this, "No Branding URL found");
                if (mcBridge == null) return;

                Debug.Console(2, this, "Setting QR code URL: {0}", mcBridge.QrCodeUrl);

                mcBridge.UserCodeChanged += (o, a) => SendMcBrandingUrl(mcBridge);
                mcBridge.UserPromptedForCode += (o, a) => DisplayUserCode(mcBridge.UserCode);

                SendMcBrandingUrl(mcBridge);
            }
        }



        /// <summary>
        /// Displays the code for the specified duration
        /// </summary>
        /// <param name="code">Mobile Control user code</param>
        private void DisplayUserCode(string code)
        {
            EnqueueCommand(
                string.Format(
                    "xcommand userinterface message alert display title:\"Mobile Control User Code:\" text:\"{0}\" duration: 30",
                    code));
        }

        private void SendMcBrandingUrl(IMobileControlRoomBridge mcBridge)
        {
            if (mcBridge == null)
            {
                return;
            }

            Debug.Console(1, this, "Sending url: {0}", mcBridge.QrCodeUrl);

            EnqueueCommand(
                "xconfiguration userinterface custommessage: \"Scan the QR code with a mobile phone to get started\"");
            EnqueueCommand(
                "xconfiguration userinterface osd halfwakemessage: \"Tap the touch panel or scan the QR code with a mobile phone to get started\"");

            var checksum = !String.IsNullOrEmpty(mcBridge.QrCodeChecksum)
                ? String.Format("checksum: {0} ", mcBridge.QrCodeChecksum)
                : String.Empty;

            EnqueueCommand(String.Format(
                "xcommand userinterface branding fetch {1}type: branding url: {0}",
                mcBridge.QrCodeUrl, checksum));
            EnqueueCommand(String.Format(
                "xcommand userinterface branding fetch {1}type: halfwakebranding url: {0}",
                mcBridge.QrCodeUrl, checksum));
        }

        private void SendBrandingUrl()
        {
            Debug.Console(1, this, "Sending url: {0}", _brandingUrl);

            EnqueueCommand(String.Format("xcommand userinterface branding fetch type: branding url: {0}",
                _brandingUrl));
            EnqueueCommand(String.Format("xcommand userinterface branding fetch type: halfwakebranding url: {0}",
                _brandingUrl));
        }

        /// <summary>
        /// Starts the HTTP feedback server and syncronizes state of codec
        /// </summary>
        /// <returns></returns>
        public override bool CustomActivate()
        {
            CrestronConsole.AddNewConsoleCommand(SetCommDebug, "SetCodecCommDebug", "0 for Off, 1 for on",
                ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(GetPhonebook, "GetCodecPhonebook",
                "Triggers a refresh of the codec phonebook", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(GetBookings, "GetCodecBookings",
                "Triggers a refresh of the booking data for today", ConsoleAccessLevelEnum.AccessOperator);

            PhonebookSyncState.InitialSyncCompleted += PhonebookSyncState_InitialSyncCompleted;
            CameraTrackingCapabilitiesChanged += CiscoCodec_CameraTrackingCapabilitiesChanged;


            //Reserved for future use
            CodecSchedule.MeetingsListHasChanged += (sender, args) => { };
            CodecSchedule.MeetingEventChange += (sender, args) => { };




            return base.CustomActivate();
        }

        private void CiscoCodec_CameraTrackingCapabilitiesChanged(object sender, CameraTrackingCapabilitiesArgs e)
        {
            if (e == null) return;
            CameraTrackingCapabilities = e.CameraTrackingCapabilites;
            SupportsCameraAutoMode = CameraTrackingCapabilities != eCameraTrackingCapabilities.None;
        }

        private void CiscoCodec_AvailableLayoutsChanged(object sender, AvailableLayoutsChangedEventArgs e)
        {
            if (e == null) return;
            AvailableLayouts = e.AvailableLayouts;
            AvailableLayoutsFeedback.FireUpdate();
        }

        private void CiscoCodec_CurrentLayoutChanged(object sender, CurrentLayoutChangedEventArgs e)
        {
            if (e == null) return;
            CurrentLayout = e.CurrentLayout;
            LocalLayoutFeedback.FireUpdate();
        }


        private void PhonebookSyncState_InitialSyncCompleted(object sender, EventArgs e)
        {
            OnDirectoryResultReturned(DirectoryRoot);
        }

        #region Overrides of Device

        public override void Initialize()
        {

            var socket = Communication as ISocketStatus;
            if (socket != null)
            {
                socket.ConnectionChange += socket_ConnectionChange;
            }

            Communication.Connect();

            CommunicationMonitor.Start();


        }

        #endregion

        private string BuildFeedbackRegistrationExpression()
        {
            const string prefix = "xFeedback register ";

            var feedbackRegistrationExpression =
                prefix + "/Configuration" + Delimiter +
                prefix + "/PresenterTrackStatus/Audio" + Delimiter +
                prefix + "/PresenterTrackStatus/MediaChannelCalls" + Delimiter +
                prefix + "/PresenterTrackStatus/ExperimentalConference/Presentation" + Delimiter +
                prefix + "/PresenterTrackStatus/ExperimentalConference/DoNotDisturb" + Delimiter +
                prefix + "/PresenterTrackStatus/Cameras/SpeakerTrack" + Delimiter +
                prefix + "/PresenterTrackStatus/Cameras/SpeakerTrack/PresenterTrackStatus" + Delimiter +
                prefix + "/PresenterTrackStatus/Cameras/SpeakerTrack/Availability" + Delimiter +
                prefix + "/PresenterTrackStatus/Cameras/PresenterTrack" + Delimiter +
                prefix + "/PresenterTrackStatus/Cameras/PresenterTrack/PresenterTrackStatus" + Delimiter +
                prefix + "/PresenterTrackStatus/Cameras/PresenterTrack/Availability" + Delimiter +
                prefix + "/PresenterTrackStatus/RoomAnalytics" + Delimiter +
                prefix + "/PresenterTrackStatus/RoomPreset" + Delimiter +
                prefix + "/PresenterTrackStatus/Standby" + Delimiter +
                prefix + "/PresenterTrackStatus/ChannelVideo/Selfview" + Delimiter +
                prefix + "/PresenterTrackStatus/MediaChannels/MediaChannelCalls" + Delimiter +
                prefix + "/PresenterTrackStatus/ChannelVideo/Layout" + Delimiter +
                //prefix + "/PresenterTrackStatus/ChannelVideo/Layout/LayoutFamily" + Delimiter +
                //prefix + "/PresenterTrackStatus/ChannelVideo/Layout/LayoutFamily/Local" + Delimiter +
                //prefix + "/PresenterTrackStatus/ChannelVideo/Layout/CurrentLayouts" + Delimiter +
                //prefix + "/PresenterTrackStatus/ChannelVideo/Layout/CurrentLayouts/AvailableLayouts" + Delimiter +
                prefix + "/PresenterTrackStatus/ChannelVideo/Input/MainVideoMute" + Delimiter +
                prefix + "/Bookings" + Delimiter +
                prefix + "/Event/Bookings" + Delimiter +
                prefix + "/Event/CameraPresetListUpdated" + Delimiter +
                prefix + "/Event/UserInterface/Presentation/ExternalSource/Selected/SourceIdentifier" + Delimiter +
                prefix + "/Event/CallDisconnect" + Delimiter;
            // Keep CallDisconnect last to detect when feedback registration completes correctly
            return feedbackRegistrationExpression;
        }


        /// <summary>
        /// Fires when initial codec sync is completed.  Used to then send commands to get call history, phonebook, bookings, etc.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SyncState_InitialSyncCompleted(object sender, EventArgs e)
        {
            Debug.Console(0, this, "InitialSyncComplete - There are {0} Active Calls", ActiveCalls.Count);
            if (ActiveCalls.Count < 1)
            {
                OnCallStatusChange(new CodecActiveCallItem()
                {
                    Name = String.Empty,
                    Number = String.Empty,
                    Type = eCodecCallType.Unknown,
                    Status = eCodecCallStatus.Unknown,
                    Direction = eCodecCallDirection.Unknown,
                    Id = String.Empty
                });
            }
            // Check for camera config info first
            if (_config.CameraInfo.Count > 0)
            {
                Debug.Console(0, this, "Reading codec cameraInfo from config properties.");
                SetUpCameras(_config.CameraInfo);
            }
            else
            {
                Debug.Console(0, this,
                    "No cameraInfo defined in video codec config.  Attempting to get camera info from codec status data");
                try
                {
                    var cameraInfo = new List<CameraInfo>();

                    Debug.Console(0, this, "Codec reports {0} cameras", CodecStatus.Status.Cameras.CameraList.Count);

                    foreach (var camera in CodecStatus.Status.Cameras.CameraList)
                    {
                        Debug.Console(0, this,
                            @"CameraList CiscoCallId: {0}
Name: {1}
ConnectorID: {2}"
                            , camera.CameraId
                            , camera.Manufacturer.Value
                            , camera.Model.Value);

                        var id = Convert.ToUInt16(camera.CameraId);
                        var info = new CameraInfo()
                        {
                            CameraNumber = id,
                            Name = string.Format("{0} {1}", camera.Manufacturer.Value, camera.Model.Value),
                            SourceId = camera.DetectedConnector.DetectedConnectorId
                        };
                        cameraInfo.Add(info);
                    }

                    Debug.Console(0, this, "Successfully got cameraInfo for {0} cameras from codec.", cameraInfo.Count);

                    SetUpCameras(cameraInfo);
                }
                catch (Exception ex)
                {
                    Debug.Console(2, this, "Error generating camera info from codec status data: {0}", ex);
                }
            }

            //CommDebuggingIsOn = false;

            GetCallHistory();

            PhonebookRefreshTimer = new CTimer(CheckCurrentHour, 3600000, 3600000);
            // check each hour to see if the phonebook should be downloaded
            GetPhonebook(null);

            BookingsRefreshTimer = new CTimer(GetBookings, 900000, 900000);
            // 15 minute timer to check for new booking info
            GetBookings(null);

            // Fire the ready event
            SetIsReady();
        }

        public void SetCommDebug(string s)
        {
            if (s == "1")
            {
                CommDebuggingIsOn = true;
                Debug.Console(0, this, "Comm Debug Enabled.");
            }
            else
            {
                CommDebuggingIsOn = false;
                Debug.Console(0, this, "Comm Debug Disabled.");
            }
        }

        private void socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs e)
        {
            Debug.Console(1, this, "Socket status change {0}", e.Client.ClientStatus);
            if (e.Client.IsConnected)
            {
                if (!_syncState.LoginMessageWasReceived)
                    _loginMessageReceivedTimer = new CTimer(o => DisconnectClientAndReconnect(), 5000);
            }
            else
            {
                _syncState.CodecDisconnected();
                PhonebookSyncState.CodecDisconnected();

                if (PhonebookRefreshTimer != null)
                {
                    PhonebookRefreshTimer.Stop();
                    PhonebookRefreshTimer = null;
                }

                if (BookingsRefreshTimer != null)
                {
                    BookingsRefreshTimer.Stop();
                    BookingsRefreshTimer = null;
                }
            }
        }

        private void DisconnectClientAndReconnect()
        {
            Debug.Console(1, this, "Retrying connection to codec.");

            Communication.Disconnect();

            _retryConnectionTimer = new CTimer(o => Communication.Connect(), 2000);

            //CrestronEnvironment.Sleep(2000);

            //Communication.Connect();
        }

        /// <summary>
        /// Gathers responses from the codec (including the delimiter.  Responses are checked to see if they contain JSON data and if so, the data is collected until a complete JSON
        /// message is received before forwarding the message to be deserialized.
        /// </summary>
        /// <param name="dev"></param>
        /// <param name="args"></param>
        private void Port_LineReceived(object dev, GenericCommMethodReceiveTextArgs args)
        {
            if (CommDebuggingIsOn)
            {
                if (!_jsonFeedbackMessageIsIncoming)
                    Debug.Console(1, this, "RX: '{0}'", ComTextHelper.GetDebugText(args.Text));
            }

            if (args.Text.ToLower().Contains("xcommand"))
            {
                Debug.Console(1, this, "Received command echo response.  Ignoring");
                return;
            }

            if (args.Text == "{" + Delimiter) // Check for the beginning of a new JSON message
            {
                _jsonFeedbackMessageIsIncoming = true;

                if (CommDebuggingIsOn)
                    Debug.Console(1, this, "Incoming JSON message...");

                _jsonMessage = new StringBuilder();
            }
            else if (args.Text == "}" + Delimiter) // Check for the end of a JSON message
            {
                _jsonFeedbackMessageIsIncoming = false;

                _jsonMessage.Append(args.Text);

                if (CommDebuggingIsOn)
                    Debug.Console(1, this, "Complete JSON Received:\n{0}", _jsonMessage.ToString());

                // Enqueue the complete message to be deserialized

                _receiveQueue.Enqueue(new ProcessStringMessage(_jsonMessage.ToString(), DeserializeResponse));

                return;
            }

            if (_jsonFeedbackMessageIsIncoming)
            {
                _jsonMessage.Append(args.Text);

                //Debug.Console(1, this, "Building JSON:\n{0}", JsonMessage.ToString());
                return;
            }

            if (!_syncState.InitialSyncComplete)
            {
                switch (args.Text.Trim().ToLower()) // remove the whitespace
                {
                    case "*r login successful":
                    {
                        _syncState.LoginMessageReceived();

                        if (_loginMessageReceivedTimer != null)
                            _loginMessageReceivedTimer.Stop();

                        //SendText("echo off");
                        SendText("xPreferences outputmode json");
                        break;
                    }
                    case "xpreferences outputmode json":
                    {
                        if (_syncState.JsonResponseModeSet)
                            return;

                        _syncState.JsonResponseModeMessageReceived();

                        if (!_syncState.InitialStatusMessageWasReceived)
                            SendText("xStatus");
                        break;
                    }
                    case "xfeedback register /event/calldisconnect":
                    {
                        _syncState.FeedbackRegistered();
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Enqueues a command to be sent to the codec.
        /// </summary>
        /// <param name="command"></param>
        public void EnqueueCommand(string command)
        {
            _syncState.AddCommandToQueue(command);
        }

        /// <summary>
        /// Appends the delimiter and send the command to the codec.
        /// Should not be used for sending general commands to the codec.  Use EnqueueCommand instead.
        /// Should be used to get initial PresenterTrackStatus and Configuration as well as set up Feedback Registration
        /// </summary>
        /// <param name="command"></param>
        public void SendText(string command)
        {
            if (CommDebuggingIsOn)
                Debug.Console(1, this, "Sending: '{0}'", ComTextHelper.GetDebugText(command + Delimiter));

            Communication.SendText(command + Delimiter);
        }

        private Version GetVersion(string actual)
        {
            Debug.Console(2, this, "Codec Firmware Version String == {0}", actual);
            var semanticActual = actual.Split('.').ToList();
            var actualInt = semanticActual.ConvertAll(s => Int16.Parse(Regex.Replace(s, "[^0-9.]", "")));

            var actualVer = new Version(actualInt[0], actualInt[1], actualInt[2], actualInt[3]);

            Debug.Console(2, this, "Codec Decoded Firmware Version String == {0}", actualVer.ToString());

            return actualVer;
        }

        private void UpdateLayoutList()
        {
            var layoutData = new List<CodecCommandWithLabel>();
            if (CodecStatus.Status.Video.Layout.CurrentLayouts.AvailableLayouts != null)
            {
                foreach (var i in CodecStatus.Status.Video.Layout.CurrentLayouts.AvailableLayouts)
                {
                    var r = i;
                    Debug.Console(2, "Adding New layout {0}", r.LayoutName.Value);
                    layoutData.Add(new CodecCommandWithLabel(r.LayoutName.Value,
                        r.LayoutName.Value));
                }
            }
            AvailableLayouts = layoutData;
            AvailableLayoutsFeedback.FireUpdate();

        }

        private void UpdateCurrentLayout()
        {
            //Debug.Console(0, this, "Active Layouts Received Change");

            CurrentLayout = CodecStatus.Status.Video.Layout.CurrentLayouts.ActiveLayout.Value;
            LocalLayoutFeedback.FireUpdate();

        }

        private void ClearLayouts()
        {
            var nullLayout = new CurrentLayouts()
            {
                AvailableLayouts = new List<LayoutData>(),
                AvailableLayoutsCount = new AvailableLayoutsCount() { Value = 0 },
                ActiveLayout = new ActiveLayout() { Value = "" }
            };

            CodecStatus.Status.Video.Layout.CurrentLayouts = nullLayout;

            AvailableLayouts = new List<CodecCommandWithLabel>();
            AvailableLayoutsFeedback.FireUpdate();
            CurrentLayout = string.Empty;
            LocalLayoutFeedback.FireUpdate();
            OnAvailableLayoutsChanged(new List<CodecCommandWithLabel>());
            OnCurrentLayoutChanged(String.Empty);


        }

        private void RegisterSystemUnitEvents()
        {
            CodecStatus.Status.SystemUnit.SystemUnitSoftware.DisplayName.ValueChangedAction +=
                () =>
                {   
                    var splitSoftware = CodecStatus.Status.SystemUnit.SystemUnitSoftware.DisplayName.Value.Split(' ');
                    Debug.Console(2, this, "Splitsoftware = {0}", splitSoftware[1]);
                    CodecFirmware = GetVersion(splitSoftware[1]);

                    OnCodecInfoChanged(new CodecInfoChangedEventArgs(eCodecInfoChangeType.Firmware)
                    {
                        Firmware = splitSoftware[1]
                    });
                    if (DeviceInfo == null) return;
                    DeviceInfo.FirmwareVersion = splitSoftware[1];
                    UpdateDeviceInfo();
                    _syncState.InitialSoftwareVersionMessageReceived();

                };

            CodecStatus.Status.SystemUnit.SystemUnitSoftware.OptionKeys.MultiSite.ValueChangedAction +=
                () =>
                    OnCodecInfoChanged(new CodecInfoChangedEventArgs(eCodecInfoChangeType.Multisite)
                    {
                        MultiSiteOptionIsEnabled = CodecStatus.Status.SystemUnit.SystemUnitSoftware.OptionKeys.MultiSite.BoolValue
                    });

            CodecStatus.Status.SystemUnit.Hardware.Module.SerialNumber.ValueChangedAction +=
                () =>
                {
                    OnCodecInfoChanged(new CodecInfoChangedEventArgs(eCodecInfoChangeType.SerialNumber)
                    {
                        SerialNumber = CodecStatus.Status.SystemUnit.Hardware.Module.SerialNumber.Value
                    });
                    if (DeviceInfo == null) return;
                    DeviceInfo.SerialNumber = CodecStatus.Status.SystemUnit.Hardware.Module.SerialNumber.Value;
                    UpdateDeviceInfo();
                };



        }

        private void RegisterVideoEvents()
        {
            /*
            CodecStatus.PresenterTrackStatus.ChannelVideo.Layout.CurrentLayouts.AvailableLayoutsCount.ValueChangedAction += () =>
            {
                Debug.Console(0, this, "Layouts Received Change");
                var layoutData = new List<CodecCommandWithLabel>();
                if (CodecStatus.PresenterTrackStatus.ChannelVideo.Layout.CurrentLayouts.AvailableLayouts != null)
                {
                    foreach (var i in CodecStatus.PresenterTrackStatus.ChannelVideo.Layout.CurrentLayouts.AvailableLayouts)
                    {
                        var r = i;
                        Debug.Console(2, "Adding New layout {0}", r.LayoutName.Value);
                        layoutData.Add(new CodecCommandWithLabel(r.LayoutName.Value,
                            r.LayoutName.Value));
                    }
                }
                AvailableLayouts = layoutData;
                AvailableLayoutsFeedback.FireUpdate();

            };
        


            CodecStatus.PresenterTrackStatus.ChannelVideo.Layout.CurrentLayouts.ActiveLayout.ValueChangedAction += () =>
            {
                Debug.Console(0, this, "Active Layouts Received Change");

                CurrentLayout = CodecStatus.PresenterTrackStatus.ChannelVideo.Layout.CurrentLayouts.ActiveLayout.Value;
                LocalLayoutFeedback.FireUpdate();
            };
             */
        }

        private void DeserializeResponse(string response)
        {
            try
            {
                //// Serializer settings.  We want to ignore null values and missing members
                //JsonSerializerSettings settings = new JsonSerializerSettings();
                //settings.NullValueHandling = NullValueHandling.Ignore;
                //settings.MissingMemberHandling = MissingMemberHandling.Ignore;
                //settings.ObjectCreationHandling = ObjectCreationHandling.Auto;

                if (response.IndexOf("\"PresenterTrackStatus\":{") > -1 || response.IndexOf("\"PresenterTrackStatus\": {") > -1)
                {
                    // PresenterTrackStatus DiagnosticsMessage

                    // Temp object so we can inpsect for call data before simply deserializing
                    var tempCodecStatus = new CiscoCodecStatus.RootObject();

                    JsonConvert.PopulateObject(response, tempCodecStatus);

                    var status = tempCodecStatus.Status;



                    if (status.SystemUnit != null)
                    {
                        JsonConvert.PopulateObject(response, CodecStatus.Status.SystemUnit);
                    }
                    if (status.Cameras != null)
                    {
                        Debug.Console(0, this, "Deserialize Cameras");
                        JsonConvert.PopulateObject(response, CodecStatus.Status.Cameras);
                    }


                    var network = status.Networks;

                    if (network != null)
                    {
                        var myNetwork = network.FirstOrDefault(i => i.NetworkId == "1");
                        if (myNetwork != null)
                        {
                            var hostname = myNetwork.Cdp.DeviceId.Value.NullIfEmpty() ?? "Unknown";
                            var ipAddress = myNetwork.IPv4.Address.Value.NullIfEmpty() ?? "Unknown";
                            var macAddress = myNetwork.Ethernet.MacAddress.Value.NullIfEmpty()
                                             ?? "Unknown";


                            OnCodecInfoChanged(new CodecInfoChangedEventArgs(eCodecInfoChangeType.Network) {IpAddress = ipAddress});

                            if (DeviceInfo != null)
                            {
                                DeviceInfo.HostName = hostname;
                                DeviceInfo.IpAddress = ipAddress;
                                DeviceInfo.MacAddress = macAddress;
                                UpdateDeviceInfo();
                            }
                        }
                    }


                    var sip = status.Sip;
                    if (sip != null)
                    {
                        JsonConvert.PopulateObject(response, CodecStatus.Status.Sip);
                        if (sip.Registrations.Count > 0)
                        {
                            var sipUri = sip.Registrations[0].Uri.Value.NullIfEmpty() ?? "Unknown";
                            var match = Regex.Match(sipUri, @"(\d+)");
                            var sipPhoneNumber = match.Success ? match.Groups[1].Value : "Unknown";
                            OnCodecInfoChanged(new CodecInfoChangedEventArgs(eCodecInfoChangeType.Sip)
                            {
                                SipPhoneNumber = sipPhoneNumber,
                                SipUri = sipUri
                            });
                        }
                    }

                    if (status.Video != null && status.Video.Layout != null)
                    {
                        if (status.Video.Layout.CurrentLayouts != null)
                        {
                            //TODO [ ] PREDICTION FIX
                            //Debug.Console(0, this, "DeserializeVideo");
                            JsonConvert.PopulateObject(response, CodecStatus.Status.Video.Layout.CurrentLayouts);
                            if (status.Video.Layout.CurrentLayouts.AvailableLayouts != null)
                            {
                                UpdateLayoutList();
                            }
                            if (status.Video.Layout.CurrentLayouts.ActiveLayout != null)
                            {
                                UpdateCurrentLayout();
                                if (status.Video.Layout.CurrentLayouts.ActiveLayout.Value == String.Empty)
                                {
                                    ClearLayouts();
                                }
                            }
                        }
                        else
                        {
                            CodecPollLayouts();
                        }
                    }


                    // Check to see if the message contains /PresenterTrackStatus/ExperimentalConference/Presentation/PresentationLocalInstances and extract source value 

                    var conference = status.StatusConference;

                    if (conference != null)
                    {
                        JsonConvert.PopulateObject(response, CodecStatus.Status.StatusConference);
                    }

                    if (conference != null &&
                        (conference.Presentation != null && conference.Presentation.PresentationLocalInstances == null))
                    {

                        // Handles an empty presentation object response
                        //return;
                    }

                    if (conference != null && conference.Presentation != null)
                    {

                        if (conference.Presentation.PresentationLocalInstances != null &&
                            conference.Presentation.PresentationLocalInstances.Count > 0)
                        {
                            if (conference.Presentation.ModeValueProperty != null)
                            {
                                _presentationActive = conference.Presentation.ModeValueProperty.Value != "Off";
                                CodecPollLayouts();
                            }
                            if (!string.IsNullOrEmpty(conference.Presentation.PresentationLocalInstances[0].Ghost))
                            {
                                _presentationSource = 0;
                                _presentationLocalOnly = false;
                                _presentationLocalRemote = false;
                            }
                            else if (conference.Presentation.PresentationLocalInstances[0].SourceValueProperty != null)
                            {
                                _presentationSource = conference.Presentation.PresentationLocalInstances[0].SourceValueProperty.IntValue;

                                // Check for any values in the SendingMode property
                                if (
                                    conference.Presentation.PresentationLocalInstances.Any(
                                        (i) => !string.IsNullOrEmpty(i.SendingMode.Value)))
                                {
                                    _presentationLocalOnly =
                                        conference.Presentation.PresentationLocalInstances.Any((i) => i.SendingMode.LocalOnly);
                                    _presentationLocalRemote =
                                        conference.Presentation.PresentationLocalInstances.Any((i) => i.SendingMode.LocalRemote);
                                }
                            }

                            PresentationSourceFeedback.FireUpdate();
                            PresentationSendingLocalOnlyFeedback.FireUpdate();
                            PresentationSendingLocalRemoteFeedback.FireUpdate();
                        }
                    }

                    var calls = status.Calls ?? null;

                    // Check to see if this is a call status message received after the initial status message
                    if (calls != null && calls.Count > 0)
                    {
                        if (calls.Count == 1 && !_presentationActive)
                        {
                            Debug.Console(0, this, "CALL WITH NO PRESENTATION!!!");
                            ClearLayouts();
                        }
                                                 

                        // Iterate through the call objects in the response
                        foreach (var c in calls)
                        {
                            var call = c;

                            var currentCallType = String.Empty;

                            if (tempCodecStatus != null && tempCodecStatus.Status != null &&
                                tempCodecStatus.Status.MediaChannels != null)
                            {
                                currentCallType = tempCodecStatus.Status.MediaChannels.MediaChannelCalls == null
                                    ? null
                                    : CheckCallType(c.CallIdString,
                                        tempCodecStatus.Status.MediaChannels.MediaChannelCalls);
                            }



                            Debug.Console(0, this, "Current MediaChannelCalls Type = {0}", currentCallType);
                            var tempActiveCall = ActiveCalls.FirstOrDefault(x => x.Id.Equals(call.CallIdString));

                            if (tempActiveCall != null)
                            {

                                var changeDetected = false;

                                //var newStatus = eCodecCallStatus.Unknown;

                                // Update properties of ActiveCallItem
                                if (call.CallStatus != null)
                                    if (!string.IsNullOrEmpty(call.CallStatus.Value))
                                    {
                                        tempActiveCall.Status = CodecCallStatus.ConvertToStatusEnum(call.CallStatus.Value);
                                        tempActiveCall.IsOnHold = tempActiveCall.Status == eCodecCallStatus.OnHold;

                                        if (tempActiveCall.Status == eCodecCallStatus.Connected)
                                        {

                                            GetCallHistory();
                                        }

                                        changeDetected = true;
                                    }

                                if (call.CallType != null || currentCallType != null)
                                {

                                    tempActiveCall.Type =
                                        CodecCallType.ConvertToTypeEnum(currentCallType ?? call.CallType.Value);
                                    changeDetected = true;
                                }



                                if (call.DisplayName != null)
                                    if (!string.IsNullOrEmpty(call.DisplayName.Value))
                                    {
                                        tempActiveCall.Name = call.DisplayName.Value;
                                        changeDetected = true;
                                    }


                                if (call.Direction != null)
                                {
                                    if (!string.IsNullOrEmpty(call.Direction.Value))
                                    {
                                        tempActiveCall.Direction =
                                            CodecCallDirection.ConvertToDirectionEnum(call.Direction.Value);
                                        changeDetected = true;
                                    }
                                }
                                if (call.Duration != null)
                                {
                                    if (!string.IsNullOrEmpty(call.Duration.Value))
                                    {
                                        tempActiveCall.Duration = call.Duration.DurationValue;
                                        changeDetected = true;
                                    }
                                }
                                if (call.PlacedOnHold != null)
                                {


                                    tempActiveCall.IsOnHold = call.PlacedOnHold.BoolValue;
                                    changeDetected = true;
                                }

                                if (!changeDetected) continue;

                                SetSelfViewMode();
                                OnCallStatusChange(tempActiveCall);
                                ListCalls();
                                CodecPollLayouts();

                            }
                            else if (call.GhostString == null || call.GhostString.ToLower() == "false") // if the ghost value is present the call has ended already
                            {

                                // Create a new call item
                                var newCallItem = new CodecActiveCallItem()
                                {

                                    Id = call.CallIdString,
                                    Status = CodecCallStatus.ConvertToStatusEnum(call.CallStatus.Value),
                                    Name = call.DisplayName.Value,
                                    Number = call.RemoteNumber.Value,
                                    Type = CodecCallType.ConvertToTypeEnum(currentCallType ?? call.CallType.Value),
                                    Direction = CodecCallDirection.ConvertToDirectionEnum(call.Direction.Value),
                                    Duration = call.Duration.DurationValue,
                                    IsOnHold = call.PlacedOnHold.BoolValue,
                                };


                                // Add it to the ActiveCalls List
                                ActiveCalls.Add(newCallItem);

                                ListCalls();

                                SetSelfViewMode();

                                OnCallStatusChange(newCallItem);

                                CodecPollLayouts();

                                //ClearLayouts();

                            }

                        }

                    }
                    if (status.Audio != null)
                    {
                        JsonConvert.PopulateObject(response, CodecStatus.Status.Audio);
                    }


                    // Check for Room Preset data (comes in partial, so we need to handle these responses differently to prevent appending duplicate items
                    var tempPresets = tempCodecStatus.Status.RoomPresets;

                    if (tempPresets.Count > 0)
                    {
                        // Create temporary list to store the existing items from the CiscoCodecStatus.RoomPreset collection
                        var existingRoomPresets = new List<CiscoCodecStatus.RoomPreset>();
                        // Add the existing items to the temporary list
                        existingRoomPresets.AddRange(CodecStatus.Status.RoomPresets);
                        // Populate the CodecStatus object (this will append new values to the RoomPreset collection
                        JsonConvert.PopulateObject(response, CodecStatus);

                        var jResponse = JObject.Parse(response);


                        IList<JToken> roomPresets = jResponse["PresenterTrackStatus"]["RoomPreset"].Children().ToList();
                        // Iterate the new items in this response agains the temporary list.  Overwrite any existing items and add new ones.
                        foreach (var camPreset in tempPresets)
                        {
                            var preset = camPreset;
                            if (preset == null) continue;
                            // First fine the existing preset that matches the CiscoCallId
                            var existingPreset = existingRoomPresets.FirstOrDefault(p => p.RoomPresetId.Equals(preset.RoomPresetId));
                            if (existingPreset != null)
                            {
                                Debug.Console(1, this, "Existing Room Preset with ID: {0} found. Updating.",
                                    existingPreset.RoomPresetId);

                                JToken updatedPreset = null;

                                // Find the JToken from the response with the matching CiscoCallId
                                foreach (
                                    var jPreset in
                                        roomPresets.Where(jPreset => jPreset["CiscoCallId"].Value<string>() == existingPreset.RoomPresetId)
                                    )
                                {
                                    updatedPreset = jPreset;
                                }

                                if (updatedPreset != null)
                                {
                                    // use PopulateObject to overlay the partial data onto the existing object
                                    JsonConvert.PopulateObject(updatedPreset.ToString(), existingPreset);
                                }

                            }
                            else
                            {
                                Debug.Console(1, this, "New Room Preset with ID: {0}. Adding.", preset.RoomPresetId);
                                existingRoomPresets.Add(preset);
                            }
                        }

                        // Replace the list in the CodecStatus object with the processed list
                        CodecStatus.Status.RoomPresets = existingRoomPresets;

                        // Generecise the list
                        NearEndPresets =
                            existingRoomPresets.GetGenericPresets<CiscoCodecStatus.RoomPreset, CodecRoomPreset>();

                        var handler = CodecRoomPresetsListHasChanged;
                        if (handler != null)
                        {
                            handler(this, new EventArgs());
                        }
                    }
                        
                    else
                    {
                        JsonConvert.PopulateObject(response, CodecStatus);
                    }
                        

                    if (_syncState.InitialStatusMessageWasReceived) return;
                    _syncState.InitialStatusMessageReceived();

                    if (!_syncState.InitialConfigurationMessageWasReceived)
                    {
                        Debug.Console(0, this, "Sending COnfiguration");
                        SendText("xConfiguration");
                    }
                    if (!_syncState.FeedbackWasRegistered)
                    {
                        Debug.Console(0, this, "Sending Feedback");

                        SendText(BuildFeedbackRegistrationExpression());
                    }
                }
                else if (response.IndexOf("\"Configuration\":{") > -1 || response.IndexOf("\"Configuration\": {") > -1)
                {
                    // Configuration DiagnosticsMessage
                    Debug.Console(2, this, "Parse Configuration : {0}", response);

                    JsonConvert.PopulateObject(response, CodecConfiguration);

                    if (CodecConfiguration != null)
                    {
                        var h323 = CodecConfiguration.Configuration.H323;
                        if (h323 != null)
                        {
                            if (h323.H323Alias != null)
                            {
                                var e164 = h323.H323Alias.E164.Value.NullIfEmpty() ?? "unknown";
                                var h323Id = h323.H323Alias.H323AliasId.Value.NullIfEmpty() ?? "unknown";
                                OnCodecInfoChanged(new CodecInfoChangedEventArgs(eCodecInfoChangeType.H323) {E164Alias = e164, H323Id = h323Id});
                            }
                        }
                        bool autoAnswer;
                        if (CodecConfiguration.Configuration.Conference.AutoAnswer.Mode.Value == null)
                            autoAnswer = false;
                        else
                        {
                            autoAnswer =
                                CodecConfiguration.Configuration.Conference.AutoAnswer.Mode.Value.ToLower() == "on";
                        }
                        OnCodecInfoChanged(new CodecInfoChangedEventArgs(eCodecInfoChangeType.AutoAnswer) {AutoAnswerEnabled = autoAnswer});
                    }

                    if (!_syncState.InitialConfigurationMessageWasReceived)
                    {
                        Debug.Console(2, this, "InitialConfig Received");
                        _syncState.InitialConfigurationMessageReceived();
                        if (!_syncState.InitialSoftwareVersionMessageWasReceived)
                        {
                            SendText("xStatus SystemUnit Software DisplayName");
                        }
                    }

                }
                else if (response.IndexOf("\"Event\":{") > -1 || response.IndexOf("\"Event\": {") > -1)
                {
                    Debug.Console(2, this, "Parse Event : {0}", response);
                    if (response.IndexOf("\"CallDisconnect\":{") > -1 || response.IndexOf("\"CallDisconnect\": {") > -1)
                    {
                        Debug.Console(2, this, "Parse CallDisconnect");
                        var eventReceived = new CiscoCodecEvents.RootObject();

                        JsonConvert.PopulateObject(response, eventReceived);

                        EvalutateDisconnectEvent(eventReceived);
                    }
                    else if (response.IndexOf("\"Bookings\":{") > -1 || response.IndexOf("\"Bookings\": {") > -1)
                        // The list has changed, reload it
                    {
                        Debug.Console(2, this, "Parse Bookings");

                        GetBookings(null);
                    }

                    else if (response.IndexOf("\"UserInterface\":{") > -1 ||
                             response.IndexOf("\"UserInterface\": {") > -1) // External SourceValueProperty Trigger
                    {
                        Debug.Console(2, this, "Parse UserInterface");

                        CiscoCodecEvents.RootObject eventReceived = new CiscoCodecEvents.RootObject();
                        JsonConvert.PopulateObject(response, eventReceived);
                        Debug.Console(2, this, "*** Got an External SourceValueProperty Selection {0} {1}", eventReceived,
                            eventReceived.Event.UserInterface,
                            eventReceived.Event.UserInterface.Presentation.ExternalSource.Selected
                                .SourceIdentifier.Value);

                        if (RunRouteAction != null && !_externalSourceChangeRequested)
                        {
                            RunRouteAction(
                                eventReceived.Event.UserInterface.Presentation.ExternalSource.Selected
                                    .SourceIdentifier.Value, null);
                        }

                        _externalSourceChangeRequested = false;
                    }
                }
                else if (response.IndexOf("\"CommandResponse\":{") > -1 ||
                         response.IndexOf("\"CommandResponse\": {") > -1)
                {
                    // CommandResponse DiagnosticsMessage
                    Debug.Console(2, this, "Parse CommandResponse - {0}", response);


                    if (response.IndexOf("\"CallHistoryRecentsResult\":{") > -1 ||
                        response.IndexOf("\"CallHistoryRecentsResult\": {") > -1)
                    {
                        Debug.Console(2, this, "Parse CallHistoryRecentsResult");

                        var codecCallHistory = new CiscoCallHistory.RootObject();

                        JsonConvert.PopulateObject(response, codecCallHistory);

                        CallHistory.ConvertCiscoCallHistoryToGeneric(
                            codecCallHistory.CommandResponse.CallHistoryRecentsResult.Entry);
                    }
                    else if (response.IndexOf("\"CallHistoryDeleteEntryResult\":{") > -1 ||
                             response.IndexOf("\"CallHistoryDeleteEntryResult\": {") > -1)
                    {
                        Debug.Console(2, this, "Parse GetCallHistoryDeleteEntryResult");

                        GetCallHistory();
                    }
                    else if (response.IndexOf("\"PhonebookSearchResult\":{") > -1 ||
                             response.IndexOf("\"PhonebookSearchResult\": {") > -1)
                    {
                        Debug.Console(2, this, "Parse PhonebookSearchResult");

                        var codecPhonebookResponse = new CiscoCodecExtendedPhonebook.RootObject();

                        JsonConvert.PopulateObject(response, codecPhonebookResponse);

                        if (!PhonebookSyncState.InitialPhonebookFoldersWasReceived)
                        {
                            // Check if the phonebook has any folders
                            PhonebookSyncState.InitialPhonebookFoldersReceived();

                            PhonebookSyncState.SetPhonebookHasFolders(
                                codecPhonebookResponse.CommandResponse.PhonebookSearchResult.Folder.Count >
                                0);

                            if (PhonebookSyncState.PhonebookHasFolders)
                            {
                                DirectoryRoot.AddFoldersToDirectory(
                                    CiscoCodecExtendedPhonebook.GetRootFoldersFromSearchResult(
                                        codecPhonebookResponse.CommandResponse.PhonebookSearchResult));
                            }

                            // Get the number of contacts in the phonebook
                            GetPhonebookContacts();
                        }
                        else if (!PhonebookSyncState.NumberOfContactsWasReceived)
                        {
                            // Store the total number of contacts in the phonebook
                            PhonebookSyncState.SetNumberOfContacts(
                                Int32.Parse(
                                    codecPhonebookResponse.CommandResponse.PhonebookSearchResult.ResultInfo
                                        .TotalRows.Value));

                            DirectoryRoot.AddContactsToDirectory(
                                CiscoCodecExtendedPhonebook.GetRootContactsFromSearchResult(
                                    codecPhonebookResponse.CommandResponse.PhonebookSearchResult));

                            PhonebookSyncState.PhonebookRootEntriesReceived();

                            PrintDirectory(DirectoryRoot);
                        }
                        else if (PhonebookSyncState.InitialSyncComplete)
                        {
                            var directoryResults = new CodecDirectory();

                            if (
                                codecPhonebookResponse.CommandResponse.PhonebookSearchResult.ResultInfo
                                    .TotalRows.Value != "0")
                                directoryResults =
                                    CiscoCodecExtendedPhonebook.ConvertCiscoPhonebookToGeneric(
                                        codecPhonebookResponse.CommandResponse.PhonebookSearchResult);

                            PrintDirectory(directoryResults);

                            DirectoryBrowseHistory.Add(directoryResults);

                            OnDirectoryResultReturned(directoryResults);

                        }
                    }
                    else if (response.IndexOf("\"BookingsListResult\":{") > -1
                             || response.IndexOf("\"BookingsListResult\": {") > -1)
                    {
                        Debug.Console(2, this, "Parse BookingsListResult - {0}", response);

                        var codecBookings = new CiscoExtendedCodecBookings.RootObject();

                        JsonConvert.PopulateObject(response, codecBookings);

                        if (codecBookings.CommandResponse.BookingsListResult.ResultInfo.TotalRows.Value !=
                            "0")
                            CodecSchedule.Meetings =
                                CiscoExtendedCodecBookings.GetGenericMeetingsFromBookingResult(
                                    codecBookings.CommandResponse.BookingsListResult.BookingsListResultBooking, _joinableCooldownSeconds);
                        else
                        {
                            CodecSchedule.Meetings = new List<Meeting>();
                        }

                        BookingsRefreshTimer.Reset(900000, 900000);
                    }

                }

            }
            catch (Exception ex)
            {
                Debug.Console(1, this, "Error Deserializing feedback from codec: {0}", ex);

                if (ex is JsonReaderException)
                {
                    Debug.Console(1, this, "Received malformed response from codec.");

                    //Communication.Disconnect();

                    //Initialize();
                }

            }
        }

        private static string CheckCallType(string id, IEnumerable<CiscoCodecStatus.MediaChannelCall> calls)
        {
            var currentCall = calls.FirstOrDefault(p => p.MediaChannelCallId == id);
            if (currentCall != null)
            {
                var videoChannels = currentCall.Channels.Where(x => x.Type.Value == "ChannelVideo").ToList();

                return videoChannels.All(v => v.ChannelVideo.Protocol.Value == "Off") ? "Audio" : "ChannelVideo";
            }
            return null;

        }


        /// <summary>
        /// MediaChannelCalls when directory results are updated
        /// </summary>
        /// <param name="result"></param>
        private void OnDirectoryResultReturned(CodecDirectory result)
        {
            CurrentDirectoryResultIsNotDirectoryRoot.FireUpdate();

            // This will return the latest results to all UIs.  Multiple indendent UI Directory browsing will require a different methodology
            var handler = DirectoryResultReturned;
            if (handler != null)
            {
                Debug.Console(2, this, "Directory result returned");
                handler(this, new DirectoryEventArgs()
                {
                    Directory = result,
                    DirectoryIsOnRoot = !CurrentDirectoryResultIsNotDirectoryRoot.BoolValue
                });
            }

            PrintDirectory(result);
        }

        /// <summary>
        /// Calculates the current local Layout
        /// </summary>
        private void ComputeLegacyLayout()
        {
            
            if (EnhancedLayouts) return;
            _currentLegacyLayout = _legacyLayouts.FirstOrDefault(l => l.Command.ToLower().Equals(CodecStatus.Status.Video.Layout.LayoutFamily.Local.Value.ToLower()));

            if (_currentLegacyLayout != null)
                LocalLayoutFeedback.FireUpdate();
            
        }


        private string UpdateLayoutsXSig(ICollection<CodecCommandWithLabel> layoutList)
        {
            //Debug.Console(0, this, "UPDATE LAYOUT XSIG!!!");
            var layouts = layoutList;
            var layoutIndex = 1;
            var tokenArray = new XSigToken[layouts.Count];
            if (layouts.Count == 0)
            {
                //Debug.Console(0, this, "NO LAYOUTS IN XSIG!!!");
                var clearBytes = XSigHelpers.ClearOutputs();
                return Encoding.GetEncoding(XSigEncoding).GetString(clearBytes, 0, clearBytes.Length);
            }


            foreach (var layout in layouts)
            {
                var arrayIndex = layoutIndex - 1;
                Debug.Console(2, this, "Layout Name : {0}", layout.Label);

                tokenArray[arrayIndex] = new XSigSerialToken(layoutIndex, layout.Label);
                layoutIndex++;
            }

            return GetXSigString(tokenArray);
        }

        private static string GetXSigString(XSigToken[] tokenArray)
        {
            string returnString;
            using (var s = new MemoryStream())
            {
                using (var tw = new XSigTokenStreamWriter(s, true))
                {
                    tw.WriteXSigData(tokenArray);
                }

                var xSig = s.ToArray();

                returnString = Encoding.GetEncoding(XSigEncoding).GetString(xSig, 0, xSig.Length);
            }

            return returnString;
        }

        public void CodecPollLayouts()
        {
            EnqueueCommand("xStatus ChannelVideo Layout CurrentLayouts");
        }


        /// <summary>
        /// Evaluates an event received from the codec
        /// </summary>
        /// <param name="eventReceived"></param>
        private void EvalutateDisconnectEvent(CiscoCodecEvents.RootObject eventReceived)
        {
            Debug.Console(0, this, "DISCONNECTEVENT!");
            if (eventReceived.Event.CallDisconnect == null) return;
            var tempActiveCall =
                ActiveCalls.FirstOrDefault(c => c.Id.Equals(eventReceived.Event.CallDisconnect.CallId.Value));
            if(tempActiveCall != null) Debug.Console(0, this, "DISCONNECT CALL {0}!", tempActiveCall.Id);
            else Debug.Console(0, this, "NO CALL MATCH!");

            // Remove the call from the Active calls list
            if (tempActiveCall == null) return;
            ActiveCalls.Remove(tempActiveCall);

            ListCalls();

            SetSelfViewMode();
            // Notify of the call disconnection
            SetNewCallStatusAndFireCallStatusChange(eCodecCallStatus.Disconnected, tempActiveCall);

            GetCallHistory();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="selector"></param>
        public override void ExecuteSwitch(object selector)
        {
            if (selector as Action == null) return;
            (selector as Action)();
            _presentationSourceKey = selector.ToString();
        }

        /// <summary>
        /// This is necessary for devices that are "routers" in the middle of the path, even though it only has one output and 
        /// may only have one input.
        /// </summary>
        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
        {
            ExecuteSwitch(inputSelector);
            _presentationSourceKey = inputSelector.ToString();
        }


        /// <summary>
        /// Gets the ID of the last connected call
        /// </summary>
        /// <returns></returns>
        public string GetCallId()
        {
            string callId = null;

            if (ActiveCalls.Count > 1)
            {
                var lastCallIndex = ActiveCalls.Count - 1;
                callId = ActiveCalls[lastCallIndex].Id;
            }
            else if (ActiveCalls.Count == 1)
                callId = ActiveCalls[0].Id;

            return callId;

        }

        public void GetCallHistory()
        {
            EnqueueCommand("xCommand CallHistory Recents Limit: 20 Order: OccurrenceTime");
        }

        /// <summary>
        /// Required for IHasScheduleAwareness
        /// </summary>
        public void GetSchedule()
        {
            GetBookings(null);
        }

        /// <summary>
        /// Gets the bookings for today
        /// </summary>
        /// <param name="command"></param>
        public void GetBookings(object command)
        {
            Debug.Console(1, this, "Retrieving BookingsListResultBooking Info from Codec. Current Time: {0}", DateTime.Now.ToLocalTime());

            EnqueueCommand("xCommand Bookings List Days: 1 DayOffset: 0");
        }

        /// <summary>
        /// Checks to see if it is 2am (or within that hour) and triggers a download of the phonebook
        /// </summary>
        /// <param name="o"></param>
        public void CheckCurrentHour(object o)
        {
            if (DateTime.Now.Hour == 2)
            {
                Debug.Console(1, this, "Checking hour to see if phonebook should be downloaded.  Current hour is {0}",
                    DateTime.Now.Hour);

                GetPhonebook(null);
                PhonebookRefreshTimer.Reset(3600000, 3600000);
            }
        }

        /// <summary>
        /// Triggers a refresh of the codec phonebook
        /// </summary>
        /// <param name="command">Just to allow this method to be called from a console command</param>
        public void GetPhonebook(string command)
        {
            PhonebookSyncState.CodecDisconnected();

            DirectoryRoot = new CodecDirectory();

            GetPhonebookFolders();
        }

        private void GetPhonebookFolders()
        {
            // Get Phonebook Folders (determine local/corporate from config, and set results limit)
            EnqueueCommand(string.Format("xCommand Phonebook Search PhonebookType: {0} ContactType: Folder",
                _phonebookMode));
        }

        private void GetPhonebookContacts()
        {
            // Get Phonebook Folders (determine local/corporate from config, and set results limit)
            EnqueueCommand(string.Format(
                "xCommand Phonebook Search PhonebookType: {0} ContactType: Contact Limit: {1}", _phonebookMode,
                _phonebookResultsLimit));
        }

        /// <summary>
        /// Searches the codec phonebook for all contacts matching the search string
        /// </summary>
        /// <param name="searchString"></param>
        public void SearchDirectory(string searchString)
        {
            Debug.Console(2, this,
                "_phonebookAutoPopulate = {0}, searchString = {1}, _lastSeached = {2}, _phonebookInitialSearch = {3}",
                _phonebookAutoPopulate ? "true" : "false", searchString, _lastSearched,
                _phonebookInitialSearch ? "true" : "false");

            if (!_phonebookAutoPopulate && searchString == _lastSearched && !_phonebookInitialSearch) return;
            EnqueueCommand(
                string.Format(
                    "xCommand Phonebook Search SearchString: \"{0}\" PhonebookType: {1} ContactType: Contact Limit: {2}",
                    searchString, _phonebookMode, _phonebookResultsLimit));
            _lastSearched = searchString;
            _phonebookInitialSearch = false;
        }


        /// <summary>
        /// // Get contents of a specific folder in the phonebook
        /// </summary>
        /// <param name="folderId"></param>
        public void GetDirectoryFolderContents(string folderId)
        {
            EnqueueCommand(
                string.Format("xCommand Phonebook Search FolderId: {0} PhonebookType: {1} ContactType: Any Limit: {2}",
                    folderId, _phonebookMode, _phonebookResultsLimit));
        }

        /// <summary>
        /// Sets the parent folder contents or the directory root as teh current directory and fires the event. Used to browse up a level
        /// </summary>
        /// <returns></returns>
        public void GetDirectoryParentFolderContents()
        {
            var currentDirectory = new CodecDirectory();

            if (DirectoryBrowseHistory.Count > 0)
            {
                var lastItemIndex = DirectoryBrowseHistory.Count - 1;
                var parentDirectoryContents = DirectoryBrowseHistory[lastItemIndex];

                DirectoryBrowseHistory.Remove(DirectoryBrowseHistory[lastItemIndex]);

                currentDirectory = parentDirectoryContents;

            }
            else
            {
                currentDirectory = DirectoryRoot;
            }

            OnDirectoryResultReturned(currentDirectory);
        }

        /// <summary>
        /// Clears the session browse history and fires the event with the directory root
        /// </summary>
        public void SetCurrentDirectoryToRoot()
        {
            DirectoryBrowseHistory.Clear();

            OnDirectoryResultReturned(DirectoryRoot);
        }

        /// <summary>
        /// Prints the directory to console
        /// </summary>
        /// <param name="directory"></param>
        private void PrintDirectory(CodecDirectory directory)
        {
            if (Debug.Level > 0)
            {
                Debug.Console(1, this, "Directory Results:\n");

                foreach (DirectoryItem item in directory.CurrentDirectoryResults)
                {
                    if (item is DirectoryFolder)
                    {
                        Debug.Console(1, this, "[+] {0}", item.Name);
                    }
                    else if (item is DirectoryContact)
                    {
                        Debug.Console(1, this, "{0}", item.Name);
                    }
                }
                Debug.Console(1, this, "Directory is on Root Level: {0}",
                    !CurrentDirectoryResultIsNotDirectoryRoot.BoolValue);
            }

        }

        /// <summary>
        /// Simple dial method
        /// </summary>
        /// <param name="number"></param>
        public override void Dial(string number)
        {
            EnqueueCommand(string.Format("xCommand Dial Number: \"{0}\"", number));
        }

        /// <summary>
        /// Dials a specific meeting
        /// </summary>
        /// <param name="meeting"></param>
        public override void Dial(Meeting meeting)
        {
            foreach (Call c in meeting.Calls)
            {
                Dial(c.Number, c.Protocol, c.CallRate, c.CallType, meeting.Id);
            }
        }

        /// <summary>
        /// Detailed dial method
        /// </summary>
        /// <param name="number"></param>
        /// <param name="protocol"></param>
        /// <param name="callRate"></param>
        /// <param name="callType"></param>
        /// <param name="meetingId"></param>
        public void Dial(string number, string protocol, string callRate, string callType, string meetingId)
        {
            EnqueueCommand(
                string.Format("xCommand Dial Number: \"{0}\" Protocol: {1} CallRate: {2} CallType: {3} BookingId: {4}",
                    number, protocol, callRate, callType, meetingId));
        }


        public override void EndCall(CodecActiveCallItem activeCall)
        {
            EnqueueCommand(string.Format("xCommand MediaChannelCalls Disconnect CallId: {0}", activeCall.Id));
            PresentationStates = eCodecPresentationStates.LocalOnly;
        }

        public override void EndAllCalls()
        {
            foreach (CodecActiveCallItem activeCall in ActiveCalls)
            {
                EnqueueCommand(string.Format("xCommand MediaChannelCalls Disconnect CallId: {0}", activeCall.Id));
            }
            PresentationStates = eCodecPresentationStates.LocalOnly;
        }

        public override void AcceptCall(CodecActiveCallItem item)
        {
            EnqueueCommand("xCommand MediaChannelCalls Accept");
        }

        public override void RejectCall(CodecActiveCallItem item)
        {
            EnqueueCommand("xCommand MediaChannelCalls Reject");
        }

        #region IHasCallHold Members

        public void HoldCall(CodecActiveCallItem activeCall)
        {
            EnqueueCommand(string.Format("xCommand MediaChannelCalls Hold CallId: {0}", activeCall.Id));
        }

        public void ResumeCall(CodecActiveCallItem activeCall)
        {
            EnqueueCommand(string.Format("xCommand MediaChannelCalls Resume CallId: {0}", activeCall.Id));
        }

        /// <summary>
        /// Resumes all held calls
        /// </summary>
        public void ResumeAllCalls()
        {
            foreach (var codecActiveCallItem in ActiveCalls.Where(codecActiveCallItem => codecActiveCallItem.IsOnHold))
            {
                ResumeCall(codecActiveCallItem);
            }
        }

        #endregion

        #region IJoinCalls

        public void JoinCall(CodecActiveCallItem activeCall)
        {
            EnqueueCommand(string.Format("xCommand MediaChannelCalls Join CallId: {0}", activeCall.Id));
        }

        public void JoinAllCalls()
        {
            StringBuilder ids = new StringBuilder();

            foreach (var call in ActiveCalls)
            {
                if (call.IsActiveCall)
                {
                    ids.Append(string.Format(" CallId: {0}", call.Id));
                }
            }

            if (ids.Length > 0)
            {
                EnqueueCommand(string.Format("xCommand MediaChannelCalls Join {0}", ids.ToString()));
            }
        }

        #endregion

        /// <summary>
        /// Sends tones to the last connected call
        /// </summary>
        /// <param name="s"></param>
        public override void SendDtmf(string s)
        {
            EnqueueCommand(string.Format("xCommand MediaChannelCalls DTMFSend CallId: {0} DTMFString: \"{1}\"", GetCallId(), s));
        }

        /// <summary>
        /// Sends tones to a specific call
        /// </summary>
        /// <param name="s"></param>
        /// <param name="activeCall"></param>
        public override void SendDtmf(string s, CodecActiveCallItem activeCall)
        {
            EnqueueCommand(string.Format("xCommand MediaChannelCalls DTMFSend CallId: {0} DTMFString: \"{1}\"", activeCall.Id, s));
        }

        public void SelectPresentationSource(int source)
        {
            _desiredPresentationSource = source;

            StartSharing();
        }

        /// <summary>
        /// Sets the ringtone volume level
        /// </summary>
        /// <param name="volume">level from 0 - 100 in increments of 5</param>
        public void SetRingtoneVolume(int volume)
        {
            if (volume < 0 || volume > 100)
            {
                Debug.Console(0, this, "Cannot set ringtone volume to '{0}'. Value must be between 0 - 100", volume);
                return;
            }

            if (volume%5 != 0)
            {
                Debug.Console(0, this,
                    "Cannot set ringtone volume to '{0}'. Value must be between 0 - 100 and a multiple of 5", volume);
                return;
            }

            EnqueueCommand(string.Format("xConfiguration Audio SoundsAndAlerts RingVolume: {0}", volume));
        }

        /// <summary>
        /// Select source 1 as the presetnation source
        /// </summary>
        public void SelectPresentationSource1()
        {
            SelectPresentationSource(2);
        }

        /// <summary>
        /// Select source 2 as the presetnation source
        /// </summary>
        public void SelectPresentationSource2()
        {
            SelectPresentationSource(3);
        }



        /// <summary>
        /// Starts presentation sharing
        /// </summary>
        public override void StartSharing()
        {
            if (_desiredPresentationSource > 0)
                EnqueueCommand(string.Format("xCommand Presentation Start PresentationSource: {0} SendingMode: {1}",
                    _desiredPresentationSource, PresentationStates.ToString()));
        }

        /// <summary>
        /// Stops sharing the current presentation
        /// </summary>
        public override void StopSharing()
        {
            _desiredPresentationSource = 0;

            EnqueueCommand("xCommand Presentation Stop");
        }



        public override void PrivacyModeOn()
        {
            EnqueueCommand("xCommand Audio MicrophoneList Mute");
        }

        public override void PrivacyModeOff()
        {
            EnqueueCommand("xCommand Audio MicrophoneList Unmute");
        }

        public override void PrivacyModeToggle()
        {
            EnqueueCommand("xCommand Audio MicrophoneList ToggleMute");
        }

        public override void MuteOff()
        {
            EnqueueCommand("xCommand Audio Volume Unmute");
        }

        public override void MuteOn()
        {
            EnqueueCommand("xCommand Audio Volume Mute");
        }

        public override void MuteToggle()
        {
            EnqueueCommand("xCommand Audio Volume ToggleMute");
        }

        /// <summary>
        /// Increments the voluem
        /// </summary>
        /// <param name="pressRelease"></param>
        public override void VolumeUp(bool pressRelease)
        {
            EnqueueCommand("xCommand Audio Volume Increase");
        }

        /// <summary>
        /// Decrements the volume
        /// </summary>
        /// <param name="pressRelease"></param>
        public override void VolumeDown(bool pressRelease)
        {
            EnqueueCommand("xCommand Audio Volume Decrease");
        }

        /// <summary>
        /// Scales the level and sets the codec to the specified level within its range
        /// </summary>
        /// <param name="level">level from slider (0-65535 range)</param>
        public override void SetVolume(ushort level)
        {
            var scaledLevel = CrestronEnvironment.ScaleWithLimits(level, 65535, 0, 100, 0);
            EnqueueCommand(string.Format("xCommand Audio Volume Set Level: {0}", scaledLevel));
        }

        /// <summary>
        /// Recalls the default volume on the codec
        /// </summary>
        public void VolumeSetToDefault()
        {
            EnqueueCommand("xCommand Audio Volume SetToDefault");
        }

        /// <summary>
        /// Puts the codec in standby mode
        /// </summary>
        public override void StandbyActivate()
        {
            EnqueueCommand("xCommand Standby Activate");
        }

        /// <summary>
        /// Wakes the codec from standby
        /// </summary>
        public override void StandbyDeactivate()
        {
            EnqueueCommand("xCommand Standby Deactivate");
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new CiscoCodecJoinMap(joinStart);

            var customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);

            if (customJoins != null)
            {
                joinMap.SetCustomJoinData(customJoins);
            }

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            LinkVideoCodecToApi(this, trilist, joinMap);

            LinkCiscoCodecToApi(trilist, joinMap);
        }

        public void LinkCiscoCodecToApi(BasicTriList trilist, CiscoCodecJoinMap joinMap)
        {
            // Custom commands to codec
            trilist.SetStringSigAction(joinMap.CommandToDevice.JoinNumber, EnqueueCommand);

            var dndCodec = this as IHasDoNotDisturbMode;
            dndCodec.DoNotDisturbModeIsOnFeedback.LinkInputSig(
                trilist.BooleanInput[joinMap.ActivateDoNotDisturbMode.JoinNumber]);
            dndCodec.DoNotDisturbModeIsOnFeedback.LinkComplementInputSig(
                trilist.BooleanInput[joinMap.DeactivateDoNotDisturbMode.JoinNumber]);

            trilist.SetSigFalseAction(joinMap.ActivateDoNotDisturbMode.JoinNumber,
                dndCodec.ActivateDoNotDisturbMode);
            trilist.SetSigFalseAction(joinMap.DeactivateDoNotDisturbMode.JoinNumber,
                dndCodec.DeactivateDoNotDisturbMode);
            trilist.SetSigFalseAction(joinMap.ToggleDoNotDisturbMode.JoinNumber,
                dndCodec.ToggleDoNotDisturbMode);

            /*
            trilist.SetSigFalseAction(joinMap.DialMeeting1.JoinNumber, () =>
            {
                const int mtg = 1;
                const int index = mtg - 1;
                Debug.Console(1, this,
                    "Meeting {0} Selected (EISC dig-o{1}) > _currentMeetings[{2}].OrganizerId: {3}, Title: {4}",
                    mtg, joinMap.DialMeeting1.JoinNumber, index, _currentMeetings[index].OrganizerId,
                    _currentMeetings[index].Title);
                if (_currentMeetings[index] != null)
                    Dial(_currentMeetings[index]);
            });
            trilist.SetSigFalseAction(joinMap.DialMeeting2.JoinNumber, () =>
            {
                const int mtg = 2;
                const int index = mtg - 1;
                Debug.Console(1, this,
                    "Meeting {0} Selected (EISC dig-o{1}) > _currentMeetings[{2}].OrganizerId: {3}, Title: {4}",
                    mtg, joinMap.DialMeeting2.JoinNumber, index, _currentMeetings[index].OrganizerId,
                    _currentMeetings[index].Title);
                if (_currentMeetings[index] != null)
                    Dial(_currentMeetings[index]);
            });
            trilist.SetSigFalseAction(joinMap.DialMeeting3.JoinNumber, () =>
            {
                const int mtg = 3;
                const int index = mtg - 1;
                Debug.Console(1, this,
                    "Meeting {0} Selected (EISC dig-o{1}) > _currentMeetings[{2}].OrganizerId: {3}, Title: {4}",
                    mtg, joinMap.DialMeeting3.JoinNumber, index, _currentMeetings[index].OrganizerId,
                    _currentMeetings[index].Title);
                if (_currentMeetings[index] != null)
                    Dial(_currentMeetings[index]);
            });
            trilist.SetSigFalseAction(joinMap.DialMeeting4.JoinNumber, () =>
            {
                const int mtg = 4;
                const int index = mtg - 1;
                Debug.Console(1, this,
                    "Meeting {0} Selected (EISC dig-o{1}) > _currentMeetings[{2}].OrganizerId: {3}, Title: {4}",
                    mtg, joinMap.DialMeeting4.JoinNumber, index, _currentMeetings[index].OrganizerId,
                    _currentMeetings[index].Title);
                if (_currentMeetings[index] != null)
                    Dial(_currentMeetings[index]);
            });

            trilist.SetSigFalseAction(joinMap.DialMeeting5.JoinNumber, () =>
            {
                const int mtg = 5;
                const int index = mtg - 1;
                Debug.Console(1, this,
                    "Meeting {0} Selected (EISC dig-o{1}) > _currentMeetings[{2}].OrganizerId: {3}, Title: {4}",
                    mtg, joinMap.DialMeeting5.JoinNumber, index, _currentMeetings[index].OrganizerId,
                    _currentMeetings[index].Title);
                if (_currentMeetings[index] != null)
                    Dial(_currentMeetings[index]);
            });
             */
            trilist.SetSigFalseAction(joinMap.DialActiveMeeting.JoinNumber, () =>
            {
                if (_currentMeeting == null) return;
                Debug.Console(1, this, "Active Meeting Selected  > _Id: {0}, Title: {1}",
                    _currentMeeting.Id, _currentMeeting.Title);
                Dial(_currentMeeting);
            });

            var halfwakeCodec = this as IHasHalfWakeMode;
            halfwakeCodec.StandbyIsOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.ActivateStandby.JoinNumber]);
            halfwakeCodec.StandbyIsOnFeedback.LinkComplementInputSig(
                trilist.BooleanInput[joinMap.DeactivateStandby.JoinNumber]);
            halfwakeCodec.HalfWakeModeIsOnFeedback.LinkInputSig(
                trilist.BooleanInput[joinMap.ActivateHalfWakeMode.JoinNumber]);
            halfwakeCodec.EnteringStandbyModeFeedback.LinkInputSig(
                trilist.BooleanInput[joinMap.EnteringStandbyMode.JoinNumber]);

            trilist.SetSigFalseAction(joinMap.ActivateStandby.JoinNumber, halfwakeCodec.StandbyActivate);
            trilist.SetSigFalseAction(joinMap.DeactivateStandby.JoinNumber, halfwakeCodec.StandbyDeactivate);
            trilist.SetSigFalseAction(joinMap.ActivateHalfWakeMode.JoinNumber,
                halfwakeCodec.HalfwakeActivate);

            CameraTrackingCapabilitiesChanged += (sender, args) =>
            {

                switch (CameraTrackingCapabilities)
                {
                    case eCameraTrackingCapabilities.None:
                    {
                        trilist.SetBool(joinMap.SpeakerTrackAvailable.JoinNumber, false);
                        trilist.SetBool(joinMap.PresenterTrackAvailable.JoinNumber, false);
                        trilist.SetBool(joinMap.CameraSupportsAutoMode.JoinNumber, false);
                        break;
                    }
                    case eCameraTrackingCapabilities.PresenterTrack:
                    {
                        trilist.SetBool(joinMap.SpeakerTrackAvailable.JoinNumber, false);
                        trilist.SetBool(joinMap.PresenterTrackAvailable.JoinNumber, true);
                        trilist.SetBool(joinMap.CameraSupportsAutoMode.JoinNumber, true);
                        break;
                    }
                    case eCameraTrackingCapabilities.SpeakerTrack:
                    {
                        trilist.SetBool(joinMap.SpeakerTrackAvailable.JoinNumber, true);
                        trilist.SetBool(joinMap.PresenterTrackAvailable.JoinNumber, false);
                        trilist.SetBool(joinMap.CameraSupportsAutoMode.JoinNumber, true);
                        break;
                    }
                    case eCameraTrackingCapabilities.Both:
                    {
                        trilist.SetBool(joinMap.SpeakerTrackAvailable.JoinNumber, true);
                        trilist.SetBool(joinMap.PresenterTrackAvailable.JoinNumber, true);
                        trilist.SetBool(joinMap.CameraSupportsAutoMode.JoinNumber, true);
                        break;
                    }

                }
            };



            AvailableLayoutsChanged += (sender, args) =>
            {
                Debug.Console(2, this, "AvailableLayoutsChanged Event");
                var layouts = args.AvailableLayouts;

                Debug.Console(2, this, "There are {0} layouts", layouts.Count);


                var clearBytes = XSigHelpers.ClearOutputs();

                trilist.SetString(joinMap.AvailableLayoutsFb.JoinNumber,
                    Encoding.GetEncoding(XSigEncoding).GetString(clearBytes, 0, clearBytes.Length));

                var availableLayoutsXSig = UpdateLayoutsXSig(layouts);

                Debug.Console(2, this, "LayoutXsig = {0}", availableLayoutsXSig);

                trilist.SetString(joinMap.AvailableLayoutsFb.JoinNumber, availableLayoutsXSig);
            };

            CurrentLayoutChanged += (sender, args) =>
            {
                var currentLayout = args.CurrentLayout;

                Debug.Console(2, this, "CurrentLayout == {0}", currentLayout == String.Empty ? "None" : currentLayout);

                trilist.SetString(joinMap.CurrentLayoutStringFb.JoinNumber, currentLayout);

            };

            CodecInfoChanged += (sender, args) =>
            {
                if (args.InfoChangeType == eCodecInfoChangeType.Unknown) return;
                switch (args.InfoChangeType)
                {
                    case eCodecInfoChangeType.Network:
                        trilist.SetString(joinMap.DeviceIpAddresss.JoinNumber, args.IpAddress);
                        break;
                    case eCodecInfoChangeType.Sip:
                        trilist.SetString(joinMap.SipPhoneNumber.JoinNumber, args.SipPhoneNumber);
                        trilist.SetString(joinMap.SipUri.JoinNumber, args.SipUri);
                        break;
                    case eCodecInfoChangeType.H323:
                        trilist.SetString(joinMap.E164Alias.JoinNumber, args.E164Alias);
                        trilist.SetString(joinMap.H323Id.JoinNumber, args.H323Id);
                        break;
                    case eCodecInfoChangeType.Multisite:
                        trilist.SetBool(joinMap.MultiSiteOptionIsEnabled.JoinNumber, args.MultiSiteOptionIsEnabled);
                        break;
                    case eCodecInfoChangeType.AutoAnswer:
                        trilist.SetBool(joinMap.AutoAnswerEnabled.JoinNumber, args.AutoAnswerEnabled);
                        break;
                }
            };


            AvailableLayoutsFeedback.LinkInputSig(trilist.StringInput[joinMap.AvailableLayoutsFb.JoinNumber]);

            trilist.SetStringSigAction(joinMap.SelectLayout.JoinNumber, LayoutSet);



            trilist.SetSigTrueAction(joinMap.ResumeAllCalls.JoinNumber, ResumeAllCalls);

            // Ringtone volume
            trilist.SetUShortSigAction(joinMap.RingtoneVolume.JoinNumber, (u) => SetRingtoneVolume(u));
            RingtoneVolumeFeedback.LinkInputSig(trilist.UShortInput[joinMap.RingtoneVolume.JoinNumber]);

            // Presentation SourceValueProperty
            trilist.SetUShortSigAction(joinMap.PresentationSource.JoinNumber, (u) => SelectPresentationSource(u));
            PresentationSourceFeedback.LinkInputSig(trilist.UShortInput[joinMap.PresentationSource.JoinNumber]);

            trilist.SetSigTrueAction(joinMap.PresentationLocalOnly.JoinNumber, SetPresentationLocalOnly);
            trilist.SetSigTrueAction(joinMap.PresentationLocalRemote.JoinNumber, SetPresentationLocalRemote);
            trilist.SetSigTrueAction(joinMap.PresentationLocalRemoteToggle.JoinNumber, SetPresentationLocalRemoteToggle);

            PresentationViewDefaultFeedback.LinkInputSig(
                trilist.BooleanInput[joinMap.PresentationViewDefault.JoinNumber]);
            PresentationViewMinimizedFeedback.LinkInputSig(
                trilist.BooleanInput[joinMap.PresentationViewMinimized.JoinNumber]);
            PresentationViewMaximizedFeedback.LinkInputSig(
                trilist.BooleanInput[joinMap.PresentationViewMaximized.JoinNumber]);

            trilist.SetSigTrueAction(joinMap.PresentationViewDefault.JoinNumber, PresentationViewDefaultSet);
            trilist.SetSigTrueAction(joinMap.PresentationViewMinimized.JoinNumber, PresentationViewMinimizedzedSet);
            trilist.SetSigTrueAction(joinMap.PresentationViewMaximized.JoinNumber, PresentationViewMaximizedSet);

            PresentationSendingLocalOnlyFeedback.LinkInputSig(
                trilist.BooleanInput[joinMap.PresentationLocalOnly.JoinNumber]);
            PresentationSendingLocalRemoteFeedback.LinkInputSig(
                trilist.BooleanInput[joinMap.PresentationLocalRemote.JoinNumber]);

            //PresenterTrackAvailableFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PresenterTrackEnabled.JoinNumber]);

            PresenterTrackStatusOffFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PresenterTrackOff.JoinNumber]);
            PresenterTrackStatusFollowFeedback.LinkInputSig(
                trilist.BooleanInput[joinMap.PresenterTrackFollow.JoinNumber]);
            PresenterTrackStatusBackgroundFeedback.LinkInputSig(
                trilist.BooleanInput[joinMap.PresenterTrackBackground.JoinNumber]);
            PresenterTrackStatusPersistentFeedback.LinkInputSig(
                trilist.BooleanInput[joinMap.PresenterTrackPersistent.JoinNumber]);

            trilist.SetSigTrueAction(joinMap.PresenterTrackOff.JoinNumber, PresenterTrackOff);
            trilist.SetSigTrueAction(joinMap.PresenterTrackFollow.JoinNumber, PresenterTrackFollow);
            trilist.SetSigTrueAction(joinMap.PresenterTrackBackground.JoinNumber, PresenterTrackBackground);
            trilist.SetSigTrueAction(joinMap.PresenterTrackPersistent.JoinNumber, PresenterTrackPersistent);

            PresentationActiveFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PresentationActive.JoinNumber]);

            CodecSchedule.MeetingEventChange += (sender, args) =>
            {
                if (_scheduleCheckTimer == null) _scheduleCheckTimer = new CTimer(ScheduleTimeCheck, null, 0, 15000);

                if (args.ChangeType != eMeetingEventChangeType.Unknown)
                {
                    UpdateMeetingsListEnhanced(this, trilist, joinMap);
                }
            };

            MinuteChanged += (sender, args) =>
            {
                if (args.EventTime == DateTime.MinValue) return;
                _scheduleCheckLast = args.EventTime;
                UpdateMeetingsListEnhanced(this, trilist, joinMap);
            };
        }


        private void UpdateMeetingsListEnhanced(IHasScheduleAwareness codec, BasicTriList trilist,
            CiscoCodecJoinMap joinMap)
        {
            var currentTime = DateTime.Now;
            const string boilerplate1 = "Available for ";
            const string boilerplate2 = "Next meeting in ";



            _currentMeetings =
                codec.CodecSchedule.Meetings.Where(m => m.StartTime >= currentTime || m.EndTime >= currentTime).ToList();

            if (_currentMeetings.Count == 0)
            {
                trilist.SetBool(joinMap.CodecAvailable.JoinNumber, true);
                trilist.SetBool(joinMap.CodecMeetingBannerActive.JoinNumber, false);
                trilist.SetBool(joinMap.CodecMeetingBannerWarning.JoinNumber, false);
                trilist.SetString(joinMap.AvailableTimeRemaining.JoinNumber, boilerplate1 + "the rest of the day.");
                trilist.SetString(joinMap.TimeToNextMeeting.JoinNumber, "No Meetings Scheduled");
                trilist.SetString(joinMap.ActiveMeetingDataXSig.JoinNumber, UpdateActiveMeetingXSig(null));
                trilist.SetUshort(joinMap.TotalMinutesUntilMeeting.JoinNumber, 0);
                trilist.SetUshort(joinMap.HoursUntilMeeting.JoinNumber, 0);
                trilist.SetUshort(joinMap.MinutesUntilMeeting.JoinNumber, 0);
                return;
            }

            var upcomingMeeting =
                _currentMeetings.FirstOrDefault(x => x.StartTime >= currentTime && x.EndTime >= currentTime);
            var currentMeeting =
                _currentMeetings.FirstOrDefault(
                    x => x.StartTime - x.MeetingWarningMinutes <= currentTime && x.EndTime >= currentTime);
            var warningBanner = upcomingMeeting != null &&
                                upcomingMeeting.StartTime - currentTime <= upcomingMeeting.MeetingWarningMinutes;



            _currentMeeting = currentMeeting;
            trilist.SetBool(joinMap.CodecAvailable.JoinNumber, currentMeeting == null);
            trilist.SetBool(joinMap.CodecMeetingBannerActive.JoinNumber, currentMeeting != null);
            trilist.SetBool(joinMap.CodecMeetingBannerWarning.JoinNumber, warningBanner);
            string availabilityMessage;
            string nextMeetingMessage;

            if (upcomingMeeting == null) return;
            var timeRemainingAvailable = upcomingMeeting.StartTime - currentTime;
            var hoursRemainingAvailable = timeRemainingAvailable.Hours;
            var minutesRemainingAvailable = timeRemainingAvailable.Minutes;
            var totalMinutesRemainingAvailable = timeRemainingAvailable.TotalMinutes;
            var hoursPlural = hoursRemainingAvailable > 1;
            var hoursPresent = hoursRemainingAvailable > 0;
            var minutesPlural = minutesRemainingAvailable > 1;
            var minutesPresent = minutesRemainingAvailable > 0;
            var hourString = String.Format("{0} {1}", hoursRemainingAvailable,
                hoursPlural ? "hours" : "hour");
            var minuteString = String.Format("{0}{1} {2}", hoursPresent ? " and " : String.Empty,
                minutesRemainingAvailable,
                minutesPlural ? "minutes" : "minute");
            var messageBase = String.Format("{0}{1}", hoursPresent ? hourString : String.Empty,
                minutesPresent ? minuteString : String.Empty);


            if (totalMinutesRemainingAvailable > 0)
            {
                availabilityMessage = String.Format("{0}{1}.", boilerplate1, messageBase);
                nextMeetingMessage = String.Format("{0}{1}.", boilerplate2, messageBase);

            }
            else
            {
                availabilityMessage = "Unavailable";
                nextMeetingMessage = "Next meeting starts soon.";
            }

            trilist.SetString(joinMap.ActiveMeetingDataXSig.JoinNumber, UpdateActiveMeetingXSig(currentMeeting));
            trilist.SetUshort(joinMap.TotalMinutesUntilMeeting.JoinNumber, (ushort) totalMinutesRemainingAvailable);
            trilist.SetUshort(joinMap.HoursUntilMeeting.JoinNumber, (ushort) hoursRemainingAvailable);
            trilist.SetUshort(joinMap.MinutesUntilMeeting.JoinNumber, (ushort) minutesRemainingAvailable);
            trilist.SetString(joinMap.AvailableTimeRemaining.JoinNumber, availabilityMessage);
            trilist.SetString(joinMap.TimeToNextMeeting.JoinNumber, nextMeetingMessage);
        }





        public void SetPresentationLocalOnly()
        {
            PresentationStates = eCodecPresentationStates.LocalOnly;
            StartSharing();
        }

        public void SetPresentationLocalRemote()
        {
            PresentationStates = eCodecPresentationStates.LocalRemote;
            StartSharing();
        }

        public void SetPresentationLocalRemoteToggle()
        {
            if (PresentationStates == eCodecPresentationStates.LocalRemote)
            {
                SetPresentationLocalOnly();
                return;
            }
            SetPresentationLocalRemote();
        }

        /// <summary>
        /// Reboots the codec
        /// </summary>
        public void Reboot()
        {
            EnqueueCommand("xCommand SystemUnit Boot Action: Restart");
        }

        /// <summary>
        /// Sets SelfView H323Mode based on config
        /// </summary>
        private void SetSelfViewMode()
        {
            if (!IsInCall)
            {
                SelfViewModeOff();
            }
            else
            {
                if (ShowSelfViewByDefault)
                    SelfViewModeOn();
                else
                    SelfViewModeOff();
            }
        }

        private void OnAvailableLayoutsChanged(List<CodecCommandWithLabel> availableLayouts)
        {
            if (availableLayouts == null) return;
            var handler = AvailableLayoutsChanged;
            if (handler == null) return;
            handler(this, new AvailableLayoutsChangedEventArgs()
            {
                AvailableLayouts = availableLayouts
            });
        }

        private void OnCameraTrackingCapabilitiesChanged()
        {
            var handler = CameraTrackingCapabilitiesChanged;
            if (handler == null) return;
            handler(this,
                new CameraTrackingCapabilitiesArgs(SpeakerTrackAvailableFeedbackFunc,
                    PresenterTrackAvailableFeedbackFunc));
        }

        private void OnCurrentLayoutChanged(string currentLayout)
        {
            if (String.IsNullOrEmpty(currentLayout)) return;
            var handler = CurrentLayoutChanged;
            if (handler == null) return;
            handler(this, new CurrentLayoutChangedEventArgs()
            {
                CurrentLayout = currentLayout
            });
        }


        /// <summary>
        /// Turns on Selfview H323Mode
        /// </summary>
        public void SelfViewModeOn()
        {
            EnqueueCommand("xCommand ChannelVideo Selfview Set H323Mode: On");
        }

        /// <summary>
        /// Turns off Selfview H323Mode
        /// </summary>
        public void SelfViewModeOff()
        {
            EnqueueCommand("xCommand ChannelVideo Selfview Set H323Mode: Off");
        }

        /// <summary>
        /// Toggles Selfview mode on/off
        /// </summary>
        public void SelfViewModeToggle()
        {
            string mode = string.Empty;

            if (CodecStatus.Status.Video.Selfview.SelfViewMode.BoolValue)
                mode = "Off";
            else
                mode = "On";

            EnqueueCommand(string.Format("xCommand ChannelVideo Selfview Set H323Mode: {0}", mode));
        }

        /// <summary>
        /// Sets a specified position for the selfview PIP window
        /// </summary>
        /// <param name="position"></param>
        public void SelfviewPipPositionSet(CodecCommandWithLabel position)
        {
            EnqueueCommand(string.Format("xCommand ChannelVideo Selfview Set H323Mode: On PIPPosition: {0}", position.Command));
        }

        /// <summary>
        /// Toggles to the next selfview PIP position
        /// </summary>
        public void SelfviewPipPositionToggle()
        {
            if (_currentSelfviewPipPosition != null)
            {
                var nextPipPositionIndex = SelfviewPipPositions.IndexOf(_currentSelfviewPipPosition) + 1;

                if (nextPipPositionIndex >= SelfviewPipPositions.Count)
                    // Check if we need to loop back to the first item in the list
                    nextPipPositionIndex = 0;

                SelfviewPipPositionSet(SelfviewPipPositions[nextPipPositionIndex]);
            }
        }

        /// <summary>
        /// Sets a specific local layout
        /// </summary>
        /// <param name="layout"></param>
        public void LayoutSet(CodecCommandWithLabel layout)
        {
            if (layout == null)
            {
                Debug.Console(0, this, "Unable to Recall Layout - Null CodecCommandWithLabel Object Sent");
                return;
            }
            EnqueueCommand(string.Format("xCommand ChannelVideo Layout SetLayout LayoutName: \"{0}\"", layout.Command));
            if (!EnhancedLayouts)
            {
                OnCurrentLayoutChanged(layout.Label);
            }
            

        }

        /// <summary>
        /// Sets a specific local layout
        /// </summary>
        /// <param name="layout"></param>
        public void LayoutSet(string layout)
        {
            if (String.IsNullOrEmpty(layout))
            {
                Debug.Console(1, this, "Unable to Recall Layout - Null string Sent");
                return;
            }
            EnqueueCommand(string.Format("xCommand ChannelVideo Layout SetLayout LayoutName: \"{0}\"", layout));

            
            if (!EnhancedLayouts)
            {
                OnCurrentLayoutChanged(layout);
            }
            

        }


        /// <summary>
        /// Toggles to the next local layout
        /// </summary>
        public void LocalLayoutToggle()
        {
            if (CurrentLayout == null) return;
            var nextLocalLayoutIndex =
                AvailableLayouts.IndexOf(AvailableLayouts.FirstOrDefault(l => l.Label.Equals(CurrentLayout))) + 1;

            if (nextLocalLayoutIndex >= AvailableLayouts.Count)
                // Check if we need to loop back to the first item in the list
                nextLocalLayoutIndex = 0;
            if (AvailableLayouts[nextLocalLayoutIndex] == null) return;
            LayoutSet(AvailableLayouts[nextLocalLayoutIndex]);
        }

        /// <summary>
        /// Toggles between single/prominent layouts
        /// </summary>
        public void LocalLayoutToggleSingleProminent()
        {
            if (String.IsNullOrEmpty(CurrentLayout)) return;
            if (CurrentLayout != "Prominent")
                LayoutSet(AvailableLayouts.FirstOrDefault(l => l.Label.Equals("Prominent")));
            else
                LayoutSet(AvailableLayouts.FirstOrDefault(l => l.Label.Equals("Single")));
        }

        /// <summary>
        /// 
        /// </summary>
        public void MinMaxLayoutToggle()
        {
            if (PresentationViewMaximizedFeedback.BoolValue)
            {
                PresentationViewMinimizedzedSet();
                return;
            }
            PresentationViewMaximizedSet();
        }

        public void PresentationViewDefaultSet()
        {
            _currentPresentationView = "Default";

            EnqueueCommand(string.Format("xCommand ChannelVideo PresentationView Set View: {0}", _currentPresentationView));
            PresentationViewFeedbackGroup.FireUpdate();
            //PresentationViewMaximizedFeedback.FireUpdate();
        }

        public void PresentationViewMinimizedzedSet()
        {
            _currentPresentationView = "Minimized";

            EnqueueCommand(string.Format("xCommand ChannelVideo PresentationView Set View: {0}", _currentPresentationView));
            PresentationViewFeedbackGroup.FireUpdate();

        }

        public void PresentationViewMaximizedSet()
        {
            _currentPresentationView = "Maximized";

            EnqueueCommand(string.Format("xCommand ChannelVideo PresentationView Set View: {0}", _currentPresentationView));
            PresentationViewFeedbackGroup.FireUpdate();
        }

        /// <summary>
        /// Calculates the current selfview PIP position
        /// </summary>
        private void ComputeSelfviewPipStatus()
        {
            _currentSelfviewPipPosition =
                SelfviewPipPositions.FirstOrDefault(
                    p => p.Command.ToLower().Equals(CodecStatus.Status.Video.Selfview.PipPosition.Value.ToLower()));

            if (_currentSelfviewPipPosition != null)
                SelfviewIsOnFeedback.FireUpdate();
        }

        /*
        /// <summary>
        /// Calculates the current local Layout
        /// </summary>
        void ComputeLocalLayout()
        {
            _currentLocalLayout = Layouts.FirstOrDefault(l => l.Command.ToLower().Equals(CodecStatus.PresenterTrackStatus.ChannelVideo.Layout.LayoutFamily.Local.Value.ToLower()));

            if (_currentLocalLayout != null)
                LocalLayoutFeedback.FireUpdate();
        }
         */

        public void RemoveCallHistoryEntry(CodecCallHistory.CallHistoryEntry entry)
        {
            EnqueueCommand(
                string.Format(
                    "xCommand CallHistory DeleteEntry CallHistoryId: {0} AcknowledgeConsecutiveDuplicates: True",
                    entry.OccurrenceHistoryId));
        }

        #region IHasCameraSpeakerTrack

        public void CameraAutoModeToggle()
        {
            if (!CameraAutoModeIsOnFeedback.BoolValue)
            {
                CameraAutoModeOn();
                return;
            }
            CameraAutoModeOff();
        }

        public void CameraAutoModeOn()
        {
            switch (CameraTrackingCapabilities)
            {
                case eCameraTrackingCapabilities.None:
                {
                    Debug.Console(0, this, "CameraList Auto H323Mode Unavailable");
                    break;
                }
                case eCameraTrackingCapabilities.PresenterTrack:
                {
                    PresenterTrackFollow();
                    break;
                }
                case eCameraTrackingCapabilities.SpeakerTrack:
                {
                    SpeakerTrackOn();
                    break;
                }
                case eCameraTrackingCapabilities.Both:
                {
                    if (PreferredTrackingMode == eCameraTrackingCapabilities.SpeakerTrack)
                    {
                        SpeakerTrackOn();
                        break;
                    }
                    PresenterTrackFollow();
                    break;
                }
            }
        }

        public void CameraAutoModeOff()
        {
            switch (CameraTrackingCapabilities)
            {
                case eCameraTrackingCapabilities.None:
                {
                    Debug.Console(0, this, "CameraList Auto H323Mode Unavailable");
                    break;
                }
                case eCameraTrackingCapabilities.PresenterTrack:
                {
                    PresenterTrackOff();
                    break;
                }
                case eCameraTrackingCapabilities.SpeakerTrack:
                {
                    SpeakerTrackOff();
                    break;
                }
                case eCameraTrackingCapabilities.Both:
                {
                    if (PreferredTrackingMode == eCameraTrackingCapabilities.SpeakerTrack)
                    {
                        SpeakerTrackOff();
                        break;
                    }
                    PresenterTrackOff();
                    break;
                }
            }

        }

        public void SpeakerTrackOn()
        {
            if (CameraIsOffFeedback.BoolValue)
            {
                CameraMuteOff();
            }

            EnqueueCommand("xCommand Cameras SpeakerTrack Activate");
        }

        public void SpeakerTrackOff()
        {
            if (CameraIsOffFeedback.BoolValue)
            {
                CameraMuteOff();
            }

            EnqueueCommand("xCommand Cameras SpeakerTrack Deactivate");

        }



        #endregion

        public void PresenterTrackOff()
        {
            if (!CodecStatus.Status.Cameras.PresenterTrack.Availability.BoolValue)
            {
                Debug.Console(0, this, "Presenter Track is Unavailable on this Codec");
                return;
            }
            if (CameraIsOffFeedback.BoolValue)
            {
                CameraMuteOff();
            }

            EnqueueCommand("xCommand Cameras PresenterTrack Set H323Mode: Off");
        }

        public void PresenterTrackFollow()
        {
            if (!CodecStatus.Status.Cameras.PresenterTrack.Availability.BoolValue)
            {
                Debug.Console(0, this, "Presenter Track is Unavailable on this Codec");
                return;
            }
            if (CameraIsOffFeedback.BoolValue)
            {
                CameraMuteOff();
            }

            EnqueueCommand("xCommand Cameras PresenterTrack Set H323Mode: Follow");
        }

        public void PresenterTrackBackground()
        {
            if (!CodecStatus.Status.Cameras.PresenterTrack.Availability.BoolValue)
            {
                Debug.Console(0, this, "Presenter Track is Unavailable on this Codec");
                return;
            }

            if (CameraIsOffFeedback.BoolValue)
            {
                CameraMuteOff();
            }

            EnqueueCommand("xCommand Cameras PresenterTrack Set H323Mode: Background");
        }

        public void PresenterTrackPersistent()
        {
            if (!CodecStatus.Status.Cameras.PresenterTrack.Availability.BoolValue)
            {
                Debug.Console(0, this, "Presenter Track is Unavailable on this Codec");
                return;
            }
            if (CameraIsOffFeedback.BoolValue)
            {
                CameraMuteOff();
            }

            EnqueueCommand("xCommand Cameras PresenterTrack Set H323Mode: Persistent");
        }




        /// <summary>
        /// Builds the cameras List.  Could later be modified to build from config data
        /// </summary>
        private void SetUpCameras(List<CameraInfo> cameraInfo)
        {
            // Add the internal camera
            Cameras = new List<CameraBase>();

            var camCount = CodecStatus.Status.Cameras.CameraList.Count;
            Debug.Console(0, this, "THERE ARE {0} CAMERAS", camCount);

            // Deal with the case of 1 or no reported cameras
            if (camCount <= 1)
            {
                var internalCamera = new CiscoCamera(Key + "-camera1", "Near End", this, 1);

                if (camCount > 0)
                {
                    // Try to get the capabilities from the codec
                    if (CodecStatus.Status.Cameras.CameraList[0] != null &&
                        CodecStatus.Status.Cameras.CameraList[0].Capabilities != null)
                    {
                        internalCamera.SetCapabilites(CodecStatus.Status.Cameras.CameraList[0].Capabilities.Options.Value);
                    }
                }

                Cameras.Add(internalCamera);
                //DeviceManager.AddDevice(internalCamera);
            }
            else
            {
                /*
                // Setup all the cameras
                for (int i = 0; i < camCount; i++)
                {
                    var cam = CodecStatus.PresenterTrackStatus.Cameras.CameraList[i];

                    var CiscoCallId = (uint) i;
                    var name = string.Format("CameraList {0}", CiscoCallId);

                    // Check for a config object that matches the camera number
                    var camInfo = cameraInfo.FirstOrDefault(c => c.CameraNumber == i + 1);
                    if (camInfo != null)
                    {
                        CiscoCallId = (uint) camInfo.SourceId;
                        name = camInfo.Name;
                    }

                    var key = string.Format("{0}-camera{1}", Key, CiscoCallId);
                    var camera = new CiscoCamera(key, name, this, CiscoCallId);

                    if (cam.StatusCapabilities != null)
                    {
                        camera.SetCapabilites(cam.StatusCapabilities.Options.Value);
                    }

                    Cameras.Add(camera);
                }
                 */

                foreach (var item in CodecStatus.Status.Cameras.CameraList)
                {
                    var cam = item;
                    var id = uint.Parse(item.CameraId);
                    var camId = id;
                    var camInfo = cameraInfo.FirstOrDefault(c => c.CameraNumber == camId);
                    var name = string.Format("CameraList {0}", camId);
                    if (camInfo != null)
                    {
                        name = camInfo.Name;
                    }

                    var key = string.Format("{0}-camera{1}", Key, camId);
                    var camera = new CiscoCamera(key, name, this, camId);

                    if (cam.Capabilities != null)
                    {
                        camera.SetCapabilites(cam.Capabilities.Options.Value);
                    }

                    Debug.Console(0, this, "Adding CameraList {0}", camera.CameraId);
                    Cameras.Add(camera);
                }
            }

            // Add the far end camera
            var farEndCamera = new CiscoFarEndCamera(Key + "-cameraFar", "Far End", this);
            Cameras.Add(farEndCamera);

            SelectedCameraFeedback = new StringFeedback(() => SelectedCamera.Key);

            ControllingFarEndCameraFeedback = new BoolFeedback(() => SelectedCamera is IAmFarEndCamera);

            NearEndPresets = new List<CodecRoomPreset>(15);

            FarEndRoomPresets = new List<CodecRoomPreset>(15);

            // Add the far end presets
            for (int i = 1; i <= FarEndRoomPresets.Capacity; i++)
            {
                var label = string.Format("Far End Preset {0}", i);
                FarEndRoomPresets.Add(new CodecRoomPreset(i, label, true, false));
            }

            Debug.Console(0, this, "Selected CameraList has key {0} and name {1}", Cameras.First().Key, Cameras.First().Name);

            SelectedCamera = Cameras.First();
            SelectCamera(SelectedCamera.Key);// call the method to select the camera and ensure the feedbacks get updated.

        }

        #region IHasCodecCameras Members

        public event EventHandler<CameraSelectedEventArgs> CameraSelected;

        public List<CameraBase> Cameras { get; private set; }

        public StringFeedback SelectedCameraFeedback { get; private set; }

        private CameraBase _selectedCamera;

        /// <summary>
        /// Returns the selected camera
        /// </summary>
        public CameraBase SelectedCamera
        {
            get { return _selectedCamera; }
            private set
            {
                _selectedCamera = value;
                SelectedCameraFeedback.FireUpdate();
                ControllingFarEndCameraFeedback.FireUpdate();
                if (CameraIsOffFeedback.BoolValue)
                    CameraMuteOff();

                var handler = CameraSelected;
                if (handler != null)
                {
                    handler(this, new CameraSelectedEventArgs(SelectedCamera));
                }
            }
        }

        public void SelectCamera(string key)
        {
            var camera = Cameras.FirstOrDefault(c => c.Key.IndexOf(key, StringComparison.OrdinalIgnoreCase) > -1);
            if (camera != null)
            {
                Debug.Console(2, this, "Selected CameraList with key: '{0}'", camera.Key);
                SelectedCamera = camera;
            }
            else
                Debug.Console(2, this, "Unable to select camera with key: '{0}'", key);

            var ciscoCam = camera as CiscoCamera;
            if (ciscoCam != null)
            {
                EnqueueCommand(string.Format("xCommand ChannelVideo Input SetMainVideoSource SourceId: {0}", ciscoCam.CameraId));
            }
        }

        public CameraBase FarEndCamera { get; private set; }

        public BoolFeedback ControllingFarEndCameraFeedback { get; private set; }

        #endregion

        #region IHasCameraPresets Members

        public event EventHandler<EventArgs> CodecRoomPresetsListHasChanged;

        public List<CodecRoomPreset> NearEndPresets { get; private set; }

        public List<CodecRoomPreset> FarEndRoomPresets { get; private set; }

        public void CodecRoomPresetSelect(int preset)
        {
            Debug.Console(1, this, "Selecting Preset: {0}", preset);
            if (SelectedCamera is IAmFarEndCamera)
                SelectFarEndPreset(preset);
            else
                EnqueueCommand(string.Format("xCommand RoomPreset Activate PresetId: {0}", preset));
        }

        public void CodecRoomPresetStore(int preset, string description)
        {
            EnqueueCommand(string.Format("xCommand RoomPreset Store PresetId: {0} Description: \"{1}\" Type: All",
                preset, description));
        }

        #endregion

        public void SelectFarEndPreset(int preset)
        {
            EnqueueCommand(
                string.Format("xCommand MediaChannelCalls FarEndControl RoomPreset Activate CallId: {0} PresetId: {1}",
                    GetCallId(), preset));
        }


        #region IHasExternalSourceSwitching Members

        /// <summary>
        /// Wheather the Cisco supports External SourceValueProperty Lists or not 
        /// </summary>
        public bool ExternalSourceListEnabled { get; private set; }

        /// <summary>
        /// The name of the RoutingInputPort to which the upstream external switcher is connected
        /// </summary>
        public string ExternalSourceInputPort { get; private set; }

        public bool BrandingEnabled { get; private set; }
        private string _brandingUrl;
        private bool _sendMcUrl;

        /// <summary>
        /// Adds an external source to the Cisco 
        /// </summary>
        /// <param name="connectorId"></param>
        /// <param name="key"></param>
        /// <param name="name"></param>
        /*public void AddExternalSource(string connectorId, string key, string name, eExternalSourceType type)
        {
            int CiscoCallId = 2;
            if (connectorId.ToLower() == "hdmiin3")
            {
                CiscoCallId = 3;
            }
            EnqueueCommand(
                string.Format(
                    "xCommand UserInterface Presentation ExternalSource Add DetectedConnectorId: {0} SourceIdentifier: \"{1}\" Name: \"{2}\" Type: {3}",
                    CiscoCallId, key, name, type.ToString()));
            // SendText(string.Format("xCommand UserInterface Presentation ExternalSource State Set SourceIdentifier: \"{0}\" State: Ready", key));
            Debug.Console(2, this, "Adding ExternalSource {0} {1}", connectorId, name);

        }
         * */
        public void AddExternalSource(string connectorId, string key, string name,
            PepperDash.Essentials.Devices.Common.VideoCodec.Cisco.eExternalSourceType type)
        {
            int id = 2;
            if (connectorId.ToLower() == "hdmiin3")
            {
                id = 3;
            }
            EnqueueCommand(
                string.Format(
                    "xCommand UserInterface Presentation ExternalSource Add DetectedConnectorId: {0} SourceIdentifier: \"{1}\" Name: \"{2}\" Type: {3}",
                    id, key, name, type.ToString()));
            // SendText(string.Format("xCommand UserInterface Presentation ExternalSource State Set SourceIdentifier: \"{0}\" State: Ready", key));
            Debug.Console(2, this, "Adding ExternalSource {0} {1}", connectorId, name);

        }


        /// <summary>
        /// Sets the state of the External SourceValueProperty 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="mode"></param>
        public void SetExternalSourceState(string key,
            PepperDash.Essentials.Devices.Common.VideoCodec.Cisco.eExternalSourceMode mode)
        {
            EnqueueCommand(
                string.Format(
                    "xCommand UserInterface Presentation ExternalSource State Set SourceIdentifier: \"{0}\" State: {1}",
                    key, mode.ToString()));
        }


        /// <summary>
        /// Clears all external sources on the codec
        /// </summary>
        public void ClearExternalSources()
        {
            EnqueueCommand("xCommand UserInterface Presentation ExternalSource RemoveAll");

        }

        /// <summary>
        /// Sets the selected source of the available external sources on teh Touch10 UI
        /// </summary>
        public void SetSelectedSource(string key)
        {
            EnqueueCommand(
                string.Format("xCommand UserInterface Presentation ExternalSource Select SourceIdentifier: {0}", key));
            _externalSourceChangeRequested = true;
        }

        /// <summary>
        /// Action that will run when the External SourceValueProperty is selected. 
        /// </summary>
        public Action<string, string> RunRouteAction { private get; set; }






        #endregion

        #region ExternalDevices



        #endregion

        #region IHasCameraOff Members

        public BoolFeedback CameraIsOffFeedback { get; private set; }

        public void CameraOff()
        {
            CameraMuteOn();
        }

        #endregion

        public BoolFeedback CameraIsMutedFeedback { get; private set; }

        /// <summary>
        /// Mutes the outgoing camera video
        /// </summary>
        public void CameraMuteOn()
        {
            EnqueueCommand("xCommand ChannelVideo Input MainVideo Mute");
        }

        /// <summary>
        /// Unmutes the outgoing camera video
        /// </summary>
        public void CameraMuteOff()
        {
            EnqueueCommand("xCommand ChannelVideo Input MainVideo Unmute");
        }

        /// <summary>
        /// Toggles the camera mute state
        /// </summary>
        public void CameraMuteToggle()
        {
            if (CameraIsMutedFeedback.BoolValue)
                CameraMuteOff();
            else
                CameraMuteOn();
        }

        #region IHasDoNotDisturbMode Members

        public BoolFeedback DoNotDisturbModeIsOnFeedback { get; private set; }

        public void ActivateDoNotDisturbMode()
        {
            EnqueueCommand("xCommand ExperimentalConference DoNotDisturb Activate");
        }

        public void DeactivateDoNotDisturbMode()
        {
            EnqueueCommand("xCommand ExperimentalConference DoNotDisturb Deactivate");
        }

        public void ToggleDoNotDisturbMode()
        {
            if (DoNotDisturbModeIsOnFeedback.BoolValue)
            {
                DeactivateDoNotDisturbMode();
            }
            else
            {
                ActivateDoNotDisturbMode();
            }
        }

        #endregion

        #region IHasHalfWakeMode Members

        public BoolFeedback HalfWakeModeIsOnFeedback { get; private set; }

        public BoolFeedback EnteringStandbyModeFeedback { get; private set; }

        public void HalfwakeActivate()
        {
            EnqueueCommand("xCommand Standby Halfwake");
        }

        #endregion


        public DeviceInfo DeviceInfo { get; set; }

        public event DeviceInfoChangeHandler DeviceInfoChanged;

        public void UpdateDeviceInfo()
        {
            var args = new DeviceInfoEventArgs(DeviceInfo);

            var raiseEvent = DeviceInfoChanged;

            if (raiseEvent == null) return;
            raiseEvent(this, args);
        }

        public void OnCodecInfoChanged(CodecInfoChangedEventArgs args)
        {
            var handler = CodecInfoChanged;
            if (handler == null) return;
            handler(this, args);
        }

    }

    public class CodecInfoChangedEventArgs : EventArgs
    {
        public bool MultiSiteOptionIsEnabled { get; set; }
        public string IpAddress { get; set; }
        public string SipPhoneNumber { get; set; }
        public string E164Alias { get; set; }
        public string H323Id { get; set; }
        public string SipUri { get; set; }
        public bool AutoAnswerEnabled { get; set; }
        public string Firmware { get; set; }
        public string SerialNumber { get; set; }

        public eCodecInfoChangeType InfoChangeType { get; private set; }

        public CodecInfoChangedEventArgs()
        {
            InfoChangeType = eCodecInfoChangeType.Unknown;
        }

        public CodecInfoChangedEventArgs(eCodecInfoChangeType changeType)
        {
            InfoChangeType = changeType;
        }


    }

    public enum eCodecInfoChangeType
    {
        AutoAnswer,
        Network,
        Sip,
        H323,
        Multisite,
        Unknown, 
        SerialNumber,
        Firmware
    }


    public class FeedbackGroup
    {
        private readonly FeedbackCollection<Feedback> _feedbacks;

        public FeedbackGroup(IEnumerable<Feedback> feedbacks)
        {
            _feedbacks = new FeedbackCollection<Feedback>();
            _feedbacks.AddRange(feedbacks);
        }

        public void FireUpdate()
        {
            foreach (var f in _feedbacks)
            {
                var feedback = f;
                feedback.FireUpdate();
            }
        }
    }

    public class MinuteChangedEventArgs : EventArgs
    {
        public DateTime EventTime { get; private set; }

        public MinuteChangedEventArgs(DateTime eventTime)
        {
            EventTime = eventTime;
        }

        public MinuteChangedEventArgs()
        {
            EventTime = DateTime.Now;
        }
    }

    public class CameraTrackingCapabilitiesArgs : EventArgs
    {
        public eCameraTrackingCapabilities CameraTrackingCapabilites { get; set; }

        public CameraTrackingCapabilitiesArgs(bool speakerTrack, bool presenterTrack)
        {
            CameraTrackingCapabilites = SetCameraTrackingCapabilities(speakerTrack, presenterTrack);
        }

        public CameraTrackingCapabilitiesArgs(Func<bool> speakerTrack, Func<bool> presenterTrack)
        {
            CameraTrackingCapabilites = SetCameraTrackingCapabilities(speakerTrack(), presenterTrack());
        }

        private eCameraTrackingCapabilities SetCameraTrackingCapabilities(bool speakerTrack, bool presenterTrack)
        {
            var trackingType = eCameraTrackingCapabilities.None;

            if (speakerTrack && presenterTrack)
            {
                trackingType = eCameraTrackingCapabilities.Both;
                return trackingType;
            }
            if (!speakerTrack && presenterTrack)
            {
                trackingType = eCameraTrackingCapabilities.PresenterTrack;
                return trackingType;
            }
            if (speakerTrack && !presenterTrack)
            {
                trackingType = eCameraTrackingCapabilities.SpeakerTrack;
                return trackingType;
            }
            return trackingType;

        }




    }
    public class CiscoCodecInfo : VideoCodecInfo
    {
        private readonly CiscoCodec _codec;

        private bool _multiSiteOptionIsEnabled;

        public override bool MultiSiteOptionIsEnabled
        {
            get { return _multiSiteOptionIsEnabled; }
        }

        private string _ipAddress;

        public override string IpAddress
        {
            get { return _ipAddress; }
        }

        private string _sipPhoneNumber;

        public override string SipPhoneNumber
        {
            get { return _sipPhoneNumber; }
        }

        private string _e164Alias;

        public override string E164Alias
        {
            get { return _e164Alias; }
        }

        private string _h323Id;

        public override string H323Id
        {
            get { return _h323Id; }
        }

        private string _sipUri;

        public override string SipUri
        {
            get { return _sipUri; }
        }

        private bool _autoAnswerEnabled;

        public override bool AutoAnswerEnabled
        {
            get { return _autoAnswerEnabled; }
        }

        public CiscoCodecInfo(CiscoCodec codec)
        {
            _codec = codec;
            _codec.CodecInfoChanged += (sender, args) =>
            {
                if (args.InfoChangeType == eCodecInfoChangeType.Unknown) return;
                switch (args.InfoChangeType)
                {
                    case eCodecInfoChangeType.Network:
                        _ipAddress = args.IpAddress;
                        break;
                    case eCodecInfoChangeType.Sip:
                        _sipPhoneNumber = args.SipPhoneNumber;
                        _sipUri = args.SipUri;
                        break;
                    case eCodecInfoChangeType.H323:
                        _h323Id = args.H323Id;
                        _e164Alias = args.E164Alias;
                        break;
                    case eCodecInfoChangeType.Multisite:
                        _multiSiteOptionIsEnabled = args.MultiSiteOptionIsEnabled;
                        break;
                    case eCodecInfoChangeType.AutoAnswer:
                        _autoAnswerEnabled = args.AutoAnswerEnabled;
                        break;
                }
            };
        }

    }


}







