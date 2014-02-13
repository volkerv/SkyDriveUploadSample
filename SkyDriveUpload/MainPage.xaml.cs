using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using System.IO.IsolatedStorage;
using Microsoft.Phone.Net.NetworkInformation;

namespace SkyDriveUpload
{
    public partial class MainPage : PhoneApplicationPage
    {
        private SkyDriveUploader mSkyDriveUploader = new SkyDriveUploader( );

        // Constructor
        public MainPage( )
        {
            InitializeComponent( );

            UpdateConnectionType( );

            DataContext = mSkyDriveUploader;
        }

        private void Upload2MB_Click( object sender, RoutedEventArgs e )
        {
            Upload( 2 );
        }

        private void Upload8MB_Click( object sender, RoutedEventArgs e )
        {
            Upload( 8 );
        }

        private void Upload30MB_Click( object sender, RoutedEventArgs e )
        {
            Upload( 30 );
        }

        private void Upload( int sizeMB )
        {
            string filePath = CreateTestFile( sizeMB );

            mSkyDriveUploader.UploadFile( filePath );
        }

        private string CreateTestFile( int sizeMB )
        {
            string filePath = string.Format( "/shared/transfers/test_{0}MB.txt", sizeMB );

            using ( IsolatedStorageFile store = IsolatedStorageFile.GetUserStoreForApplication( ) )
            {
                if ( !store.FileExists( filePath ) )
                {
                    using ( IsolatedStorageFileStream fileStream = store.CreateFile( filePath ) )
                    {
                        const int bufSize = 1024* 1024;

                        byte[] buffer =  Enumerable.Repeat((byte)'M', bufSize).ToArray( );

                        for ( int i = 0; i < sizeMB; i++ )
                        {
                            fileStream.Write( buffer, 0, bufSize );
                        }
                    }
                }
            }

            return filePath;
        }

        private void UpdateConnectionType( )
        {
            try
            {
                DeviceNetworkInformation.ResolveHostNameAsync(
                    new DnsEndPoint( "microsoft.com", 80 ),
                    new NameResolutionCallback( nrr =>
                    {
                        if ( nrr != null )
                        {
                            var info = nrr.NetworkInterface;
                            if ( info != null )
                            {
                                mSkyDriveUploader.ConnectionType = info.InterfaceType;

                                Deployment.Current.Dispatcher.BeginInvoke( ( ) =>
                                    {
                                        InterfaceNameValue.Text = info.InterfaceName;
                                        ConnectionTypeValue.Text = info.InterfaceType.ToString( );
                                        ConnectionSubTypeValue.Text = ( info.InterfaceSubtype != NetworkInterfaceSubType.Unknown ) ? info.InterfaceSubtype.ToString( ) : "";
                                    } );
                            }
                        }
                    } ), null );
            }
            catch ( Exception ex )
            {
                System.Diagnostics.Debug.WriteLine( "Failed to update connection type: " + ex.Message );
            }
        }

    }
}