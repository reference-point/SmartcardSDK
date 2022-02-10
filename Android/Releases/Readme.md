# Release notes

## 2.4.0
10/02/22 : Support for new card types 
           Date parsing fix for Android 12
           Bug fixes

## 2.3.3
10/06/21 :  Added offline read support for upcoming CSCS card stock
			Added manual card search support.

## 2.2.0 - 2.3.2
Internal releases

## 2.1.0
17/12/19 : Added support for static QR code reading

## 2.0.4
25/06/18 : Fix to add support for reading cards using NFC on LG and HTC devices

## 2.0.3
24/04/18 : Fix to NFC reading + MiM scheme added for v1 QR support

## 2.0.2
24/04/18 : Minor bug fixes

## 2.0.1
03/04/18 :  This has been removed (never distributed)

## 2.0.0
23/03/18 : First release of SDK

# Usage

## Add the following buildscript dependency to your project gradle file

    classpath "io.realm:realm-gradle-plugin:6.0.0"

## Apply the following plugin to your build gradle file

	apply plugin: 'realm-android'

## Add the following dependencies to your gradle file

    // SDK dependency (third-party)
    implementation 'io.reactivex.rxjava2:rxandroid:2.0.2'
    implementation 'io.reactivex.rxjava2:rxjava:2.1.14'
    implementation 'com.squareup.okhttp3:okhttp:3.12.2'
    implementation 'com.google.code.gson:gson:2.8.5'
    implementation 'org.apache.commons:commons-lang3:3.4'
    implementation 'com.jakewharton.timber:timber:4.6.1'

    // SDK dependency (.aar SDK library files - see project libs folder)
    implementation(name: 'smartcardsdk', ext: 'aar')
    implementation(name: 'smartcardsdk-metadata-cscs', ext: 'aar')
    implementation(name: 'smartcardsdk-common', ext: 'aar')
    implementation(name: 'smartcardsdk-render', ext: 'aar')
   

## Initialise the SDK

        SmartcardClient.init(getApplicationContext(),
                new ISmartcardClientEventHandler<InitTaskResult>() {
                    @Override
                    public void onProgressUpdate(String progressMsg) {
                        addProgress(progressMsg);
                    }
                    @Override
                    public void onPostExecute(SmartcardClientResult<InitTaskResult> result) {
                        if (result.isSuccess()) {
                            addProgress("Initialisation returned success!");
                        } else {
                            // Initialisation failed
                            SmartcardClientError e = result.getError();
                            addProgress(e != null ? e.getFullErrorMessage() : "No error info available!");
                        }
                    }
                },
                new InitTaskParams("my-api-key", "my-app-name", "my-app-version", "SDK Test App"), null);

## Read a card using NFC

        // uses the NFC Intent object returned by the OS...
        SmartcardClient.readCard(getApplicationContext(), new ISmartcardClientEventHandler() {
            @Override
            public void onProgressUpdate(String progressMsg) {
                addProgress(progressMsg);
            }

            @Override
            public void onPostExecute(SmartcardClientResult result) {
                Log.d(TAG, "SDK read card response received.");
                if (result.isSuccess())
                    processCardData(result.getResult().getCardData());
                else
                    addProgress(result.getError().getFullErrorMessage());
            }
        }, intent, enumSyncOption.SYNC);

## Read a card using QRCode

        // First read a QR code using the device's camera - set qrCodeData to the QR code data
        CardReadInfo cri = new CardReadInfo(new Date(), "Site hut 22", "London", 0, 0, null);
        ReadCardByQRCodeTaskParams params = new ReadCardByQRCodeTaskParams(qrCodeData, cri);
        SmartcardClient.readCardByQRCode(getApplicationContext(), new ISmartcardClientEventHandler<ReadCardByQRCodeTaskResult>() {
            @Override
            public void onProgressUpdate(String progressMsg) {
                addProgress(progressMsg);
            }

            @Override
            public void onPostExecute(SmartcardClientResult<ReadCardByQRCodeTaskResult> result) {
                if (result.isSuccess()) {
                    ReadCardByQRCodeTaskResult qrResult = result.getResult();
                    if (qrResult.qrCodeReadResult == enumQRCodeReadResult.SUCCESS) {
                        processCardData(result.getResult().getCardData());
                    } else if (qrResult.qrCodeReadResult == enumQRCodeReadResult.OFFLINE) {
                        // save the qr code so it can be processed when device comes back online
                        cachedQRCode = qrResult.qrCodeString;
                    } else if (qrResult.qrCodeReadResult == enumQRCodeReadResult.SERVER_REJECTED)
                        addProgress("QR code was rejected. Reason: [" + qrResult.serverRejectionReasonMessage + "]." + (qrResult.failureMessage != null ? qrResult.failureMessage : ""));
                    else
                        addProgress("QR code read FAILED. Reason: [" + qrResult.failureMessage + "].");
                } else {
                    addProgress(result.getError().getFullErrorMessage());
                }
            }
        }, params);


## Render card bitmaps

        HashMap<String, String> renderHashMap = CSCSMetadata.getRenderData(cardData)
        SmartcardClient.renderCard(getApplicationContext(), new ISmartcardClientEventHandler() {
            @Override
            public void onProgressUpdate(String progressMsg) {}

            @Override
            public void onPostExecute(SmartcardClientResult result) {
                if (!result.isSuccess()) {
                    return;
                }
                RenderCardTaskResult renderResult = (RenderCardTaskResult) result.getResult();
                if (renderResult.cardFront == null) {
                    return;
                }
                displayCard(renderResult);
            }
        }, new RenderCardTaskParams("cscs", renderHashMap, null));

        private void displayCard(RenderCardTaskResult renders) {

            byte[] byteArrayFront;
            byte[] byteArrayBack;
            try {
                byteArrayFront = getRenderBytes(renders.cardFront); 
                byteArrayBack = getRenderBytes(renders.cardBack);
            } catch (Exception ex) {
                return;
            }

            Bitmap bmp = BitmapFactory.decodeByteArray(byteArrayFront, 0, byteArrayFront.length);
            ImageView image = findViewById(R.id.img_show_card_front);
            image.setImageBitmap(bmp);
        }

        private byte[] getRenderBytes(Bitmap render) {
            ByteArrayOutputStream stream = new ByteArrayOutputStream();
            render.compress(Bitmap.CompressFormat.PNG, 100, stream);
            return stream.toByteArray();
        }
