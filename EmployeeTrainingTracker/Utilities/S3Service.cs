using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace EmployeeTrainingTracker.Helpers
{
    public static class S3Service
    {
        private static readonly IAmazonS3 _s3Client;
        private static readonly string _bucketName;

        static S3Service()
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                var accessKey = config["AWS:AccessKey"];
                var secretKey = config["AWS:SecretKey"];
                var regionName = config["AWS:Region"] ?? "eu-west-1";
                _bucketName = config["AWS:BucketName"];

                // Basic validation so you don't get null errors later
                if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
                    throw new Exception("AWS Credentials missing from appsettings.json");

                var region = RegionEndpoint.GetBySystemName(regionName);
                _s3Client = new AmazonS3Client(accessKey, secretKey, region);
            }
            catch (Exception ex)
            {
                // This will pop up if the move/config mentioned above isn't done
                System.Windows.Forms.MessageBox.Show($"S3 Configuration Error: {ex.Message}");
                throw;
            }
        }

        // Uploads a file into an employee-specific "folder"
        public static async Task<bool> UploadCertificateAsync(string localPath, string s3Key)
        {
            try
            {
                var fileTransferUtility = new TransferUtility(_s3Client);
                await fileTransferUtility.UploadAsync(localPath, _bucketName, s3Key);
                return true;
            }
            catch (Exception ex)
            {
                // THIS IS THE NEW LINE: Show us exactly what Amazon is complaining about!
                System.Windows.Forms.MessageBox.Show($"S3 Upload Error:\n{ex.Message}", "AWS Error");
                return false;
            }
        }

        // Now it just generates a link based on the exact key we saved in the DB
        public static string GetSecureViewUrl(string s3Key)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = s3Key,
                Expires = DateTime.UtcNow.AddMinutes(15)
            };

            return _s3Client.GetPreSignedURL(request);
        } 

        public static async Task DeleteCertificateAsync(string s3Key)
        {
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = s3Key
            };
            await _s3Client.DeleteObjectAsync(deleteRequest);
        }
    }
}