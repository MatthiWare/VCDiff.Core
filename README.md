<p align="center">
<a href="https://ci.appveyor.com/project/Matthiee/vcdiff-core"><img src="https://ci.appveyor.com/api/projects/status/fr1l0ktyxtofu72e?svg=true" alt="Buitld Status (AppVeyor)"></a>
<a href="https://github.com/MatthiWare/VCDiff.Core/issues"><img src="https://img.shields.io/github/issues/MatthiWare/VCDiff.Core.svg" alt="Open Issues"></a>
<a href="https://codecov.io/gh/MatthiWare/VCDiff.Core"><img src="https://codecov.io/gh/MatthiWare/VCDiff.Core/branch/master/graph/badge.svg" alt="Codecov" /></a>
<a href="https://tldrlegal.com/license/apache-license-2.0-(apache-2.0)"><img src="https://img.shields.io/badge/license-apache%202.0-blue.svg" alt="MIT License"></a>
</p>

# Open-VCDiff .NET Core Implementation

This is a full implementation of Open-VCDiff in C# based on [Google's open-vcdiff](https://github.com/google/open-vcdiff). This is written entirely in C# - no external C++ libraries required. This includes proper SDHC support with interleaving and checksums. 

It is fully compatible with Google's Open-VCDiff for encoding and decoding. 

Original work by [Metric](https://github.com/Metric)

Original repo [Metric/VCDiff](https://github.com/Metric/VCDiff)

## Requirements
- .NET Standard 2.0+ _(code)_
- .NET Core 3.1 _(CLI)_

# CLI

Using the CLI for creating delta patches and applying delta

### Create delta patch

To encode/create delta patch you need to specify `create -o [original] -n [updated] -d [output] -b [window size]`.

`dotnet .\VCDiff.Core.Cli create .\original.exe .\updated.exe .\delta -b 8`

`dotnet .\VCDiff.Core.Cli create -o .\original.exe -n .\updated.exe -d .\delta -b 8`

`dotnet .\VCDiff.Core.Cli create --old .\original.exe --new .\updated.exe --delta .\delta --buffer 8`

_[Window size]: The maximum buffer size for window chunking (in megabytes)._

### Apply delta patch

To apply delta patch you need to specify `patch [original] [delta] [output]`.

`dotnet .\VCDiff.Core.Cli patch .\original.exe .\delta .\updated.exe`

`dotnet .\VCDiff.Core.Cli patch -o .\original.exe -d .\delta -n .\updated.exe`

`dotnet .\VCDiff.Core.Cli patch --old .\original.exe --delta .\delta --new .\updated.exe`


### Verify hashes in PowerShell

- Encode/Create patch
`dotnet .\VCDiff.Core.Cli create -o .\original.exe -n .\updated.exe -d .\delta -b 8`

- Decode/Apply patch
`dotnet .\VCDiff.Core.Cli patch -o .\original.exe -d .\delta -n .\updated.exe`

- Verify hash
`get-filehash -Path ".\updated.exe", ".\updated_with_delta.exe" -algorithm MD5`


| Algorithm | Hash                             | Path                                                                           |          |
|-----------|----------------------------------|--------------------------------------------------------------------------------|----------|
| MD5       | 2BFD9E6BE33C3441D79E4EE2AC725C74 | D:\Source\Repos\VCDiff.Core\VCDiff.Core.Cli\bin\publish\updated.exe            | Original |
| MD5       | 2BFD9E6BE33C3441D79E4EE2AC725C74 | D:\Source\Repos\VCDiff.Core\VCDiff.Core.Cli\bin\publish\updated_with_delta.exe | Patched  |



# Encoding Data
The dictionary must be a file or data that is already in memory. The file must be fully read in first in order to encode properly. This is just how the algorithm works for VCDiff. The encode function is blocking.

```
using VCDiff.Include;
using VCDiff.Encoders;
using VCDiff.Shared;

void DoEncode() {
    using(FileStream output = new FileStream("...some output path", FileMode.Create, FileAccess.Write))
    using(FileStream dict = new FileStream("..dictionary / old file path", FileMode.Open, FileAccess.Read))
    using(FileStream target = new FileStream("..target data / new data path", FileMode.Open, FileMode.Read)) {
        VCCoder coder = new VCCoder(dict, target, output);
        VCDiffResult result = coder.Encode(); //encodes with no checksum and not interleaved
        if(result != VCDiffResult.SUCCESS) {
            //error was not able to encode properly
        }
    }
}

```

Encoding with checksum or interleaved or both
```
bool interleaved = true;
bool checksum = false;

coder.Encode(interleaved, checksum);

checksum = true;
coder.Encode(interleaved, checksum);
```

Modifying the default chunk size for windows

```
int windowSize = 2; //in Megabytes. The default is 1MB window chunks.

VCCoder coder = new VCCoder(dict, target, output, windowSize)
```

Modifying the default minimum copy encode size. Which means the match must be >= MinBlockSize in order to qualify as match for copying from dictionary file.
```
ChunkEncoder.MinBlockSize = 16; 
//Default is 32 bytes. Lowering this can improve the delta compression for small files. 
//Please keep it a power of 2.
//Anything lower than 2 or BlockHash.BlockSize is ignored.
```

Modifying the default BlockSize for hashing
```
BlockHash.BlockSize = 32; 
//increasing this for large files with lots of similar data can improve results.
//the default is 16. Please keep it a power of two. 
//Lowering it for small files can also improve results. 
//Anything lower than 2 will be ignored.
```

# Decoding Data
The dictionary must be a file or data that is already in memory. The file must be fully read in first in order to decode properly. This is just how the algorithm works for VCDiff.

Due note the interleaved version of a delta file is meant for streaming and it is supported by the decoder already. However, non-interleaved expects access for reading the full delta file at one time. The delta file is still streamed, but must be able to read fully in sequential order.

```
using VCDiff.Include;
using VCDiff.Decoders;
using VCDiff.Shared;

void DoDecode() {
    using(FileStream output = new FileStream("...some output path", FileMode.Create, FileAccess.Write))
    using(FileStream dict = new FileStream("..dictionary / old file path", FileMode.Open, FileAccess.Read))
    using(FileStream target = new FileStream("..delta encoded part", FileMode.Open, FileMode.Read)) {
        VCDecoder decoder = new VCDecoder(dict, target, output);

        //You must call decoder.Start() first. The header of the delta file must be available before calling decoder.Start()

        VCDiffResult result = decoder.Start();

        if(result != VCDiffResult.SUCCESS) {
            //error abort
        }

        long bytesWritten = 0;
        result = decoder.Decode(out bytesWritten);

        if(result != VCDiffResult.SUCCESS) {
            //error decoding
        }

        //if success bytesWritten will contain the number of bytes that were decoded
    }
}

```

Handling streaming of the interleaved format has the same setup. But instead you will continue calling decode until you know you have received everything. So, you will need to keep track of that. Everytime you loop through make sure you have enough data in the buffer to at least be able to decode the next VCDiff Window Header (which can be up to 22 bytes or so). After that the decode function will handle the waiting for the next part of the interleaved data for that VCDiff Window. The decode function is blocking.

```
while(bytesWritten < someSizeThatYouAreExpecting) {
    //make sure we have enough data in buffer to at least try and decode the next window section
    //otherwise we will probably receive an error.
    if(myStream.Length < 22) continue; 

    long thisChunk = 0;
    result = decoder.Decode(out thisChunk);

    bytesWritten += thisChunk;

    //yes with three Rs.
    if(result == VCDiffResult.ERRROR) {
        //it failed to decode something
        //could be an issue that the window failed to parse
        //or actual data failed to decode properly
        break;
    }

    //otherwise continue on if you get SUCCESS or EOD (End of Data);
    //because only you know when you will have the data finished loading
    //the decoder doesn't care if nothing is available and it will keep trying until more is
    //it is best to do this in a separate thread as it is blocking.
}
```

# Apache 2.0 License
This is licensed under the same license as Open-VCDiff by Google. See [Apache 2.0 license](http://www.apache.org/licenses/LICENSE-2.0).