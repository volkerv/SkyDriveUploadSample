using Microsoft.Live;
using Microsoft.Phone.Info;
using Microsoft.Phone.Net.NetworkInformation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;

namespace SkyDriveUpload
{
    public class SkyDriveUploader : INotifyPropertyChanged
    {
        private LiveAuthClient mAuthClient;
        private LiveConnectClient mLiveConnectClient;
        private LiveConnectSession mLiveConnectSession;

        private static readonly string[ ] scopes =
             new string[ ] { 
            "wl.signin", 
            "wl.skydrive_update" };

        private string mCurrentState;
        public string CurrentState 
        {
            get 
            {
                return mCurrentState; 
            }

            set
            {
                mCurrentState = value;
                NotifyPropertyChanged( "CurrentState" );
            }
        }

        private NetworkInterfaceType mConnectionType = NetworkInterfaceType.Unknown;
        public NetworkInterfaceType ConnectionType
        {
            get
            {
                return mConnectionType;
            }

            set
            {
                mConnectionType = value;
            }
        }

        public SkyDriveUploader( )
        {
        }

        public void UploadFile( string filePath )
        {
            SkyDriveLogin( filePath );
        }

        private void SkyDriveLogin( string filePath )
        {
            UpdateState( "Logging in..." );

            mAuthClient = new LiveAuthClient( YOUR_APP_ID );
            mAuthClient.InitializeCompleted += AuthClient_InitializeCompleted;
            mAuthClient.InitializeAsync( scopes, filePath );
        }

        private void AuthClient_InitializeCompleted( object sender, LoginCompletedEventArgs e )
        {
            if ( e.Status == LiveConnectSessionStatus.Connected )
            {
                HandleLoginCompleted( e.Session, (string) e.UserState );
            }
            else
            {
                mAuthClient.LoginCompleted += AuthClient_LoginCompleted;
                mAuthClient.LoginAsync( scopes, e.UserState );
            }
        }

        private void AuthClient_LoginCompleted( object sender, LoginCompletedEventArgs e )
        {
            if ( e.Status == LiveConnectSessionStatus.Connected )
            {
                UpdateState( "Log in completed" );
                HandleLoginCompleted( e.Session, ( string )e.UserState );
            }
            else
            {
                UpdateState( "Log in failed" );

                if ( e.Error != null )
                {
                    string msg = string.Format( AppResources.SignInFailure, e.Error.Message );
                    MessageBox.Show( msg, AppResources.SignInFailureTitle, MessageBoxButton.OK );
                }

                mLiveConnectClient = null;
            }
        }

        private void HandleLoginCompleted( LiveConnectSession session, string filePath )
        {
            System.Diagnostics.Debug.WriteLine( "HandleLoginCompleted" );
            mLiveConnectSession = session;

            mLiveConnectClient = new LiveConnectClient( mLiveConnectSession );

            DoUploadNow( filePath );
        }

        private void DoUploadNow( string filePath )
        {
            try
            {
                UpdateState( "Starting upload..." );

                long fileSize = 0;
                using ( IsolatedStorageFile isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication( ) )
                {
                    using ( IsolatedStorageFileStream file = isolatedStorage.OpenFile( filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite ) )
                    {
                        fileSize  = file.Length;
                    }
                }

                if ( fileSize > 0 )
                {
                    if ( CheckAndAdjustUploadTransferPreferences( fileSize ) )
                    {
                        if ( mLiveConnectClient != null )
                        {
                            ThreadPool.QueueUserWorkItem( StartUpload, filePath );
                        }
                    }
                }
            }
            catch ( Exception ex )
            {
                System.Diagnostics.Debug.WriteLine( "Failed to upload file: " +ex.Message );
            }
        }

        private void StartUpload( object o )
        {
            try
            {
                string filePath = ( string )o;

                App app = ( App )App.Current;

                string uploadFolder = "me/skydrive/";

                mLiveConnectClient.BackgroundUploadProgressChanged += LiveConnectClient_BackgroundUploadProgressChanged;
                mLiveConnectClient.BackgroundUploadCompleted += LiveConnectClient_BackgroundUploadCompleted;

                Uri uri = new Uri( filePath, UriKind.Relative );
                mLiveConnectClient.BackgroundUploadAsync( uploadFolder, uri, OverwriteOption.Overwrite );
            }
            catch ( Exception ex )
            {
                System.Diagnostics.Debug.WriteLine( "Failed to start file upload: " + ex.Message );
            }
        }

        void LiveConnectClient_BackgroundUploadCompleted( object sender, LiveOperationCompletedEventArgs e )
        {
            try
            {
                if ( e.Error != null )
                {
                    UpdateState( "Upload failed" );

                    string msg = string.Format( AppResources.FailedToUpload, e.Error.Message );
                    MessageBox.Show( msg, AppResources.FailedToUploadTitle, MessageBoxButton.OK );
                }
                else
                {
                    UpdateState( "Upload completed" );
                }

                if ( mLiveConnectClient != null )
                {
                    mLiveConnectClient.BackgroundUploadCompleted -= LiveConnectClient_BackgroundUploadCompleted;
                    mLiveConnectClient.BackgroundUploadProgressChanged -= LiveConnectClient_BackgroundUploadProgressChanged;
                }
            }
            catch ( Exception ex )
            {
                System.Diagnostics.Debug.WriteLine( "Fatal error in LiveConnectClient_BackgroundUploadCompleted: " + ex.Message );
            }
        }

        private void LiveConnectClient_BackgroundUploadProgressChanged( object sender, LiveUploadProgressChangedEventArgs e )
        {
            System.Diagnostics.Debug.WriteLine( string.Format( "{0}%", e.ProgressPercentage ) );
            UpdateState( string.Format( "{0}%", e.ProgressPercentage ) );
        }

        private bool CheckAndAdjustUploadTransferPreferences( long fileSize )
        {
            bool canContinue = true;

            if ( fileSize > 0 )
            {
                string errMessage = "";
                string errTitle = AppResources.CannotUploadTitle;

                long fileSizeMB = fileSize / 1000000;
                bool runningOnBattery = ( DeviceStatus.PowerSource == PowerSource.Battery );

                if ( mLiveConnectClient != null )
                {
                    BackgroundTransferPreferences btp = ( fileSize > 20 * 1000 * 1000 ) ? BackgroundTransferPreferences.None : BackgroundTransferPreferences.AllowCellularAndBattery;
                    mLiveConnectClient.BackgroundTransferPreferences = btp;
                }

                switch ( mConnectionType )
                {
                    case NetworkInterfaceType.Wireless80211:
                        if ( runningOnBattery )
                        {
                            canContinue = ( fileSize <= 5 * 1000 * 1000 );
                            errMessage = string.Format( AppResources.CannotUploadMsgWifiBattery, fileSizeMB );
                        }
                        else
                        {
                            canContinue = ( fileSize <= 100 * 1000 * 1000 );
                            errMessage = string.Format( AppResources.CannotUploadMsgWifi, fileSizeMB );
                        }
                        break;
                    case NetworkInterfaceType.MobileBroadbandCdma:
                    case NetworkInterfaceType.MobileBroadbandGsm:
                        canContinue = ( fileSize <= 5 * 1000 * 1000 );
                        errMessage = string.Format( AppResources.CannotUploadMsgCellular, fileSizeMB );
                        break;
                }

                if ( !canContinue )
                {
                    MessageBox.Show( errMessage, errTitle, MessageBoxButton.OK );
                    UpdateState( "Error" );
                }
            }

            return canContinue;
        }

        private void UpdateState( string newState )
        {
            Deployment.Current.Dispatcher.BeginInvoke( ( ) => CurrentState = newState );
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged( string propertyName )
        {
            if ( PropertyChanged != null )
            {
                PropertyChanged( this, new PropertyChangedEventArgs( propertyName ) );
            }
        }
    }
}
