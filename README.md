# VocaGrzmot
an unofficial Vocaloid devkit that makes Vocaloid-compatible voicebanks in an easy and intuitive way.

## Current Status & Roadmap

**The ultimate goal of VocaGrzmot is to allow users to create and export fully Vocaloid-compatible voicebanks** through an intuitive interface. 

Please note that the project is still **under active development and not yet finished**. At this stage, the exporter is not fully implemented, and **the tool currently only generates native `grzmoto.json` files**. `grzmoto.json` files are roughly equivalent to `oto.ini` files in function. Vocaloid-compatible export functionality will be introduced in future updates.

## Using the editor

Choose **WAV folder** and select a voicebank folder. The folder's top level may contain WAV audio files and, once saved, one `grzmoto.json` file; other file types are rejected. Open an existing `grzmoto.json` from that folder or create entries, referencing WAV files by filename. Selecting an entry displays its waveform with its timing boundaries overlaid; drag a vertical boundary marker to reposition it, and the timing field updates as you drag. Enter timing values separated by semicolons so decimal commas work with localized number formats (for example, `0,5; 1,5; 2`). Existing comma-separated values with decimal points are still accepted. The waveform viewer supports PCM WAV (8/16/24/32-bit) and IEEE float WAV (32/64-bit).

```json
{
  "Sil k a": ["ka.wav", 0, 100, 150, 500],
  "dZ M": ["ju.wav", 500, 600, 1000],
  "a": ["a.wav", 123, 456],
  "Sil a": ["a.wav", 1, 23, 45]
}
```

## How `grzmoto.json` Works

The `grzmoto.json` file serves as the configuration backbone for the voicebank, functioning similarly to the traditional `oto.ini` file found in UTAU/OpenUTAU, but modernized into a clean, easy-to-parse JSON structure that is better designed for Vocaloid.

It maps phonetic aliases to specific audio files and precise timing parameters (in milliseconds) required for proper waveform synthesis.

### Structure Breakdown

Every entry in the file follows this key-value format:

```json
"Phonetic Alias": ["filename.wav", phonemevalue1, phonemevalue2, ...]
```

*   **Key (Phonetic Alias):** The identifier for the phonemes or transition (e.g., `"Sil k a"` or `"dZ M"`). Spaces separate individual phonemes, allowing for clean Vocaloid-style multi-phoneme definitions.
*   **Value 0 (String):** The source audio file name (e.g., `"ka.wav"`).
*   **Other values (Number):** **Phoneme timing boundaries** – Absolute timestamps marking the transitions of phonemes. The number of timing values **must be exactly one more than the number of phonemes** present in the key. 
    *   **Stationaries** (1 phoneme, e.g., `"a"`): Must have exactly **2** timing values.
    *   **Diphones** (2 phonemes, e.g., `"dZ M"`, `"Sil a"`): Must have exactly **3** timing values.
    *   **Triphones** (3 phonemes, e.g., `"Sil k a"`): Must have exactly **4** timing values.

*Note: Any entry where the value count does not match the phoneme count (such as `"a": ["a.wav", 123, 456, 789]`) is considered invalid syntax and will result in a parsing error.*

### Timing Example

Unlike legacy engines that use abstract relative parameters (like offset, overlap, or preutterance), `grzmoto.json` utilizes literal physical phoneme boundaries on the audio timeline.

For example, given the triphone:
`"Sil k a": ["ka.wav", 0, 100, 150, 500]`

*   **0:** The timestamp marking the start of the `Sil` segment.
*   **100:** The timestamp where `Sil` ends and the `k` phoneme begins.
*   **150:** The timestamp where `k` ends and the `a` phoneme begins.
*   **500:** The timestamp marking the definitive end of the `a` phoneme segment.

Timestamps must be strictly increasing. `"Sil k a": ["ka.wav", 0, 100, 1000, 5]` is not allowed.

## How to run

Run `VocaGrzmot.cs` in an editor such as Visual Studio.

## Future implementations

1. a ddb and ddi extractor (based on [ain-soph/ddb-extraction](https://github.com/ain-soph/ddb-extraction) and [yuukawahiroshi/ddb-tools](https://github.com/yuukawahiroshi/ddb-tools)) will be added to allow modification of existing voicebanks
2. .trans and .seg generators similar to [LuciozUndiezz/VocaloidDBTool-SAK](https://github.com/LuciozUndiezz/VocaloidDBTool-SAK) will be added, but [LuciozUndiezz/VocaloidDBTool-SAK](https://github.com/LuciozUndiezz/VocaloidDBTool-SAK) is counter-intuitive as it requires strategic nonstandard otoing that diverges wildly from UTAU and is hard to eyeball, and does not have full triphone timing support. Instead of oto.ini, the .trans and .seg generators here will use grzmoto.json to directly generate .trans and .seg files.
3. voicebank package to ddb/ddi/vvd format

**I am bad at programming so please make pull requests!**

**I am bad at programming so please make pull requests!**

**I am bad at programming so please make pull requests!**

**I am bad at programming so please make pull requests!**

**I am bad at programming so please make pull requests!**

**I am bad at programming so please make pull requests!**

**I am bad at programming so please make pull requests!**

**I am bad at programming so please make pull requests!**

**I am bad at programming so please make pull requests!**

**I am bad at programming so please make pull requests!**

**I am bad at programming so please make pull requests!**

**I am bad at programming so please make pull requests!**


**I am bad at programming so please make pull requests!**

**I am bad at programming so please make pull requests!**

**I am bad at programming so please make pull requests!**

**I am bad at programming so please make pull requests!**

**I am bad at programming so please make pull requests!**

**I am bad at programming so please make pull requests!**

**I am bad at programming so please make pull requests!**

**I am bad at programming so please make pull requests!**

**I am bad at programming so please make pull requests!**
