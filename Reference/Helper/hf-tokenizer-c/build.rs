fn main() {
    csbindgen::Builder::default()
        .input_extern_file("src/lib.rs")
        .input_extern_file("src/err.rs")
        .csharp_dll_name("hf_tokenizers_c")
        .csharp_class_name("NativeMethods")
        .csharp_namespace("EveConv.HuggingFaceFastTokenizer.Raw")
        .generate_csharp_file("../EveConv.HuggingFaceFastTokenizer.Raw/NativeMethods.cs")
        .unwrap();
}
