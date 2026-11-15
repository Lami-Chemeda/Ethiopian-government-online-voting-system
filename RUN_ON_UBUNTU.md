# Run the Voting System with OCR on Ubuntu

This project uses Tesseract native libraries. Do not run it with the `dotnet-sdk`
Snap package: its isolated `core20` libc cannot load the host Tesseract libraries.

On a new Ubuntu computer, run the following once in a normal terminal:

```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0 tesseract-ocr tesseract-ocr-eng tesseract-ocr-amh libtesseract-dev libleptonica-dev
sudo snap remove dotnet-sdk
```

Close and reopen the terminal, then confirm the runtime is the system one:

```bash
command -v dotnet
dotnet --version
```

`command -v dotnet` must print `/usr/bin/dotnet`, not `/snap/bin/dotnet`.

Finally, from the `VotingSystem` directory, clear only generated build output and rebuild:

```bash
dotnet clean
dotnet restore
dotnet run
```

`dotnet clean` does not remove the source code, database, uploaded images, or
`tessdata` language files. It removes copied native binaries from a previous
machine so the application uses this computer's compatible Tesseract libraries.
