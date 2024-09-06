using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Configuration;

namespace Coinecta.Data.Services;

public class S3Service
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _awsRegion;

    public S3Service(IConfiguration configuration)
    {
        IConfigurationSection awsOptions = configuration.GetSection("AWS");
        string accessKey = awsOptions["AccessKey"] ?? throw new Exception("AWS access key not configured");
        string secretKey = awsOptions["SecretKey"] ?? throw new Exception("AWS secret key not configured");
        _awsRegion = awsOptions["Region"] ?? throw new Exception("AWS region not configured");

        AmazonS3Config config = new()
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_awsRegion)
        };

        _s3Client = new AmazonS3Client(accessKey, secretKey, config);
    }

    public async Task<bool> UploadFileAsync(string bucketName, string keyName, string filePath)
    {
        try
        {
            TransferUtility fileTransferUtility = new(_s3Client);
            await fileTransferUtility.UploadAsync(filePath, bucketName, keyName);

            return true;
        }
        catch (AmazonS3Exception e)
        {
            Console.WriteLine($"Error encountered on server. Message:'{e.Message}' when writing an object");
            return false;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Unknown encountered on server. Message:'{e.Message}' when writing an object");
            return false;
        }
    }

    // Overloaded method for uploading JSON data
    public async Task<bool> UploadJsonAsync(string bucketName, string keyName, string jsonData)
    {
        try
        {
            var byteArray = Encoding.UTF8.GetBytes(jsonData);

            using var memoryStream = new MemoryStream(byteArray);
            var fileTransferUtility = new TransferUtility(_s3Client);

            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = memoryStream,
                BucketName = bucketName,
                Key = keyName,
                ContentType = "application/json"
            };

            await fileTransferUtility.UploadAsync(uploadRequest);

            return true;
        }
        catch (AmazonS3Exception e)
        {
            Console.WriteLine($"Error encountered on server. Message:'{e.Message}' when writing an object");
            return false;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Unknown encountered on server. Message:'{e.Message}' when writing an object");
            return false;
        }
    }

    public async Task<bool> DownloadFileAsync(string bucketName, string keyName, string destinationFilePath)
    {
        try
        {
            TransferUtility fileTransferUtility = new(_s3Client);
            await fileTransferUtility.DownloadAsync(destinationFilePath, bucketName, keyName);

            return true;
        }
        catch (AmazonS3Exception e)
        {
            Console.WriteLine($"Error encountered on server. Message:'{e.Message}' when reading an object");
            return false;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Unknown encountered on server. Message:'{e.Message}' when reading an object");
            return false;
        }
    }

    public async Task<string?> DownloadJsonAsync(string bucketName, string keyName)
    {
        try
        {
            var getObjectRequest = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = keyName
            };

            using GetObjectResponse response = await _s3Client.GetObjectAsync(getObjectRequest);
            using StreamReader reader = new(response.ResponseStream);

            string jsonContent = await reader.ReadToEndAsync();
            return jsonContent;
        }
        catch (AmazonS3Exception e)
        {
            Console.WriteLine($"Error encountered on server. Message:'{e.Message}' when reading an object");
            return null;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Unknown encountered on server. Message:'{e.Message}' when reading an object");
            return null;
        }
    }
}