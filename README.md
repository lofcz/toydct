# Toy DCT
A toy implementation of the JPEG encoder &amp; decoder in .NET sans file (de)serialization - (Run-length + Huffman).

The implementation sacrifices performance for simplicity intentionally and shouldn't be used for anything but admiring the beautiful math behind JPEG, MP3, and many other formats. Interestingly, many ideas in the plain old JPEG are used in ML even today - what else are VAEs but energy compaction functions? Quantization, another concept used in JPEG, is one of the pillars enabling cost-efficient LLMs deployment and inferring with models often too computationally intensive for consumer hardware.

<img src="https://github.com/lofcz/toydct/assets/10260230/ebaa808e-f088-46e7-894b-acb7b2052420" width="192">
<img src="https://github.com/lofcz/toydct/assets/10260230/844c462f-8d6d-43f0-be78-7d5b50ededf8" width="192">
<img src="https://github.com/lofcz/toydct/assets/10260230/f0cb379a-2b77-4a30-a5a0-3736b70ae829" width="192">
<img src="https://github.com/lofcz/toydct/assets/10260230/99b68000-7333-494c-a283-069536e1c2f9" width="192">

DCT components retained in a 8x8 block: 1, 2, 8, 64.

If this sparked your interest, but the terms used above sound unfamiliar, I recommend the following videos:
- https://www.youtube.com/watch?v=0me3guauqOU
- https://www.youtube.com/watch?v=Kv1Hiv3ox8I
