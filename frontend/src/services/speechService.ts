// file made by SmDev Sm-Oslomet
import * as SpeechSDK from "microsoft-cognitiveservices-speech-sdk";

const speechKey = import.meta.env.VITE_SPEECH_KEY as string;
const speechRegion = import.meta.env.VITE_SPEECH_REGION as string;

// Speech to text
export const speechToText = (): Promise<string> => {
    return new Promise((resolve, reject) => {
        if (!speechKey || !speechRegion) {
            reject ("Missing Skeep Key or Speech Region");
            return;
        }

        const speechConfig = SpeechSDK.SpeechConfig.fromSubscription(
            speechKey,
            speechRegion
        );

        speechConfig.speechRecognitionLanguage = "nb-NO";

        const audioConfig = SpeechSDK.AudioConfig.fromDefaultMicrophoneInput();
        const recognizer = new SpeechSDK.SpeechRecognizer(
            speechConfig,
            audioConfig
        );

        SpeechSDK.Recognizer.enableTelemetry(true); // for testing
        recognizer.recognizing = (s, e) => {
        console.log("RECOGNIZING:", e.result.text);
    };

    recognizer.recognized = (s, e) => {// for testing
    console.log("RECOGNIZED:", e.result.text);
    console.log("REASON:", e.result.reason);
    };

    recognizer.canceled = (s, e) => {
    console.error("CANCELED:", e.errorDetails);
    console.error("CANCEL REASON:", e.reason);
    };

    recognizer.sessionStarted = () => { 
    console.log("SESSION STARTED");
    };

    recognizer.sessionStopped = () => { // for testing
    console.log("SESSION STOPPED");
    };

        recognizer.recognizeOnceAsync(
            (result) => {
                if (result.reason === SpeechSDK.ResultReason.RecognizedSpeech){
                    resolve(result.text);
                } else {
                    reject({ // for testing
                        reason: result.reason,
                        text: result.text,
                        errorDetauls: result.errorDetails,
                    });
                }
                recognizer.close();
            },
            (err) => {
                recognizer.close();
                reject(err);
            }
        );
    });
};

// Text to speech

export const textToSpeech = (text: string): Promise<string> => {
    return new Promise((resolve,reject) => {
        if (!speechKey || !speechRegion){
            reject("Missing speech key or speech region");
            return;
        }

        const speechConfig = SpeechSDK.SpeechConfig.fromSubscription(
            speechKey,
            speechRegion
        );

        speechConfig.speechSynthesisVoiceName = "nb-NO-FinnNeural"; // female voice: nb-NO-PernilleNeural 

        const synthesizer = new SpeechSDK.SpeechSynthesizer(speechConfig);

        synthesizer.speakTextAsync(
            text,
            (result) => {
                if (
                    result.reason ===
                    SpeechSDK.ResultReason.SynthesizingAudioCompleted
                ) {
                    const blob = new Blob([result.audioData], {
                        type: "audio/wav",
                    });
                    const url = URL.createObjectURL(blob);
                    resolve(url);
                } else {
                    reject(result.errorDetails);
                }
                synthesizer.close();
            },
            (err) => {
                synthesizer.close();
                reject(err);
            }
        );
    });
};