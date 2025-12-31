//https://github.com/mlc-ai/tokenizers-cpp/blob/55d53aa38dc8df7d9c8bd9ed50907e82ae83ce66/rust/src/lib.rs
//! A simple C wrapper of tokenzier library

pub mod err;

use ahash::AHashMap;
use serde::{Deserialize, Serialize};
use serde_json::Value;
use std::path::Path;
use std::str::FromStr;
use tokenizers::models::bpe::BPE;
use tokenizers::pre_tokenizers::byte_level::ByteLevel;
use tokenizers::tokenizer::Tokenizer;

use err::set_last_error;

#[derive(Serialize, Deserialize, Debug, Clone)]
pub struct TokenizerWrapper {
    pub(crate) tokenizer: Tokenizer,
}

pub type Vocab = AHashMap<String, u32>;
pub type Merges = Vec<(String, String)>;

#[repr(C)]
pub struct TokenizerEncodeResult {
    token_ids: *mut u32,
    len: usize,
}

#[repr(C)]
pub struct TokenizerDecodeResult {
    chars: *mut u8,
    len: usize,
}

impl TokenizerWrapper {
    pub fn from_str(json: &str) -> Result<TokenizerWrapper, tokenizers::Error> {
        let tokenizer = Tokenizer::from_str(json)?;
        Ok(Self::new(tokenizer))
    }

    pub fn from_file(path: impl AsRef<Path>) -> Result<TokenizerWrapper, tokenizers::Error> {
        let tokenizer = Tokenizer::from_file(path)?;
        Ok(Self::new(tokenizer))
    }

    pub fn from_bytes(bytes: &[u8]) -> Result<TokenizerWrapper, tokenizers::Error> {
        let tokenizer = Tokenizer::from_bytes(bytes)?;
        Ok(Self::new(tokenizer))
    }

    fn new(tokenizer: Tokenizer) -> TokenizerWrapper {
        TokenizerWrapper { tokenizer }
    }

    pub fn byte_level_bpe_from_str(
        vocab: &str,
        merges: &str,
        added_tokens: &str,
    ) -> Result<TokenizerWrapper, tokenizers::Error> {
        let out_of_range_err = |field: &str| -> tokenizers::Error {
            tokenizers::Error::from(std::boxed::Box::new(std::io::Error::new(
                std::io::ErrorKind::Other,
                format!("{} out of range", field),
            )))
        };
        let vocab_json: Value = serde_json::from_str(vocab).map_err(|e| Box::new(e))?;
        let mut vocab = ahash::AHashMap::new();
        match vocab_json {
            Value::Object(m) => {
                for (token, id) in m {
                    if let Value::Number(id) = id {
                        let id = id.as_u64().ok_or_else(|| out_of_range_err("vocab.id"))? as u32;
                        vocab.insert(token, id);
                    }
                }
            }
            _ => panic!("Invalid vocab.json file."),
        };
        if !added_tokens.is_empty() {
            let added_tokens_json: Value =
                serde_json::from_str(added_tokens).map_err(|e| Box::new(e))?;
            match added_tokens_json {
                Value::Object(m) => {
                    for (token, id) in m {
                        if let Value::Number(id) = id {
                            let id = id
                                .as_u64()
                                .ok_or_else(|| out_of_range_err("added_tokens.id"))?
                                as u32;
                            vocab.insert(token, id);
                        }
                    }
                }
                _ => panic!("Invalid added_tokens.json file."),
            };
        }

        let merges = merges
            .lines()
            .filter(|line| !line.starts_with("#version"))
            .map(|line| {
                let parts = line.split(' ').collect::<Vec<_>>();
                if parts.len() != 2 {
                    panic!("Invalid merges.txt file.")
                }
                return (parts[0].to_string(), parts[1].to_string()); // Add the `return` keyword here
            })
            .collect::<Vec<(String, String)>>();
        let byte_level = ByteLevel::new(
            /*add_prefix_space=*/ false, /*trim_offsets=*/ false,
            /*use_regex=*/ false,
        );
        let mut tokenizer = Tokenizer::new(BPE::new(vocab, merges));
        tokenizer
            .with_pre_tokenizer(Some(byte_level))
            .with_decoder(Some(byte_level));
        Ok(TokenizerWrapper {
            tokenizer: tokenizer,
        })
    }

    pub fn encode(
        &self,
        text: &str,
        add_special_tokens: bool,
    ) -> Result<Vec<u32>, tokenizers::Error> {
        let encoded = self.tokenizer.encode(text, add_special_tokens)?;
        Ok(encoded.get_ids().to_vec())
    }

    pub fn encode_batch(
        &self,
        texts: Vec<&str>,
        add_special_tokens: bool,
    ) -> Result<Vec<Vec<u32>>, tokenizers::Error> {
        let results = self
            .tokenizer
            .encode_batch(texts, add_special_tokens)?
            .into_iter()
            .map(|encoded| encoded.get_ids().to_vec())
            .collect::<Vec<Vec<u32>>>();
        Ok(results)
    }

    pub fn decode(
        &self,
        ids: &[u32],
        skip_special_tokens: bool,
    ) -> Result<String, tokenizers::Error> {
        let decoded = self.tokenizer.decode(ids, skip_special_tokens)?;
        Ok(decoded)
    }

    pub fn decode_batch(
        &self,
        sentences: &[&[u32]],
        skip_special_tokens: bool,
    ) -> Result<Vec<String>, tokenizers::Error> {
        Ok(self
            .tokenizer
            .decode_batch(sentences, skip_special_tokens)?)
    }
}

#[no_mangle]
extern "C" fn tokenizers_new_from_str(input_cstr: *const u8, len: usize) -> *mut TokenizerWrapper {
    unsafe {
        let json = &String::from_utf8_lossy(std::slice::from_raw_parts(input_cstr, len));
        let wrapper = TokenizerWrapper::from_str(json);
        match wrapper {
            Ok(w) => return Box::into_raw(Box::new(w)),
            Err(e) => {
                set_last_error(format!("Failed to create tokenizer from str: {}", e));
                return std::ptr::null_mut();
            }
        }
    }
}

#[no_mangle]
extern "C" fn tokenizers_new_from_file(
    filepath_cstr: *const u8,
    len: usize,
) -> *mut TokenizerWrapper {
    unsafe {
        let path = &String::from_utf8_lossy(std::slice::from_raw_parts(filepath_cstr, len));
        let wrapper = TokenizerWrapper::from_file(path.as_ref());
        match wrapper {
            Ok(w) => return Box::into_raw(Box::new(w)),
            Err(e) => {
                set_last_error(format!("Failed to create tokenizer from str: {}", e));
                return std::ptr::null_mut();
            }
        }
    }
}

#[no_mangle]
extern "C" fn tokenizers_new_from_bytes(
    byte_array: *const u8,
    len: usize,
) -> *mut TokenizerWrapper {
    unsafe {
        let bytes = std::slice::from_raw_parts(byte_array, len);
        let wrapper = TokenizerWrapper::from_bytes(bytes);
        match wrapper {
            Ok(w) => return Box::into_raw(Box::new(w)),
            Err(e) => {
                set_last_error(format!("Failed to create tokenizer from str: {}", e));
                return std::ptr::null_mut();
            }
        }
    }
}

#[no_mangle]
extern "C" fn byte_level_bpe_tokenizers_new_from_str(
    input_vocab_str: *const u8,
    len_vocab: usize,
    input_merges_str: *const u8,
    len_merges: usize,
    input_added_tokens_str: *const u8,
    len_added_tokens: usize,
) -> *mut TokenizerWrapper {
    unsafe {
        let vocab =
            &String::from_utf8_lossy(std::slice::from_raw_parts(input_vocab_str, len_vocab));
        let merges =
            &String::from_utf8_lossy(std::slice::from_raw_parts(input_merges_str, len_merges));
        let added_tokens = &String::from_utf8_lossy(std::slice::from_raw_parts(
            input_added_tokens_str,
            len_added_tokens,
        ));
        let wrapper = TokenizerWrapper::byte_level_bpe_from_str(vocab, merges, added_tokens);
        match wrapper {
            Ok(w) => return Box::into_raw(Box::new(w)),
            Err(e) => {
                set_last_error(format!(
                    "Failed to create ByteLevelBPE tokenizer from str: {}",
                    e
                ));
                return std::ptr::null_mut();
            }
        }
    }
}

#[no_mangle]
extern "C" fn tokenizers_encode(
    handle: *mut TokenizerWrapper,
    input_cstr: *const u8,
    len: usize,
    add_special_tokens: i32,
    out_result: *mut TokenizerEncodeResult,
) -> i32 {
    unsafe {
        let input_data = match std::str::from_utf8(std::slice::from_raw_parts(input_cstr, len)) {
            Ok(d) => d,
            Err(e) => {
                (*out_result).token_ids = std::ptr::null_mut();
                (*out_result).len = 0;
                set_last_error(format!("Invalid UTF-8 input: {}", e));
                return 1;
            }
        };
        match (*handle).encode(input_data, add_special_tokens != 0) {
            Ok(encoded) => {
                let len = encoded.len();
                *out_result = TokenizerEncodeResult {
                    token_ids: Box::into_raw(encoded.into_boxed_slice()) as *mut u32,
                    len: len,
                };
                0
            }
            Err(err) => {
                (*out_result).token_ids = std::ptr::null_mut();
                (*out_result).len = 0;
                set_last_error(format!("Failed to encode: {}", err));
                1
            }
        }
    }
}

#[no_mangle]
extern "C" fn tokenizers_encode_batch(
    handle: *mut TokenizerWrapper,
    input_cstr: *const *const u8,
    input_len: *const usize,
    num_seqs: usize,
    add_special_tokens: i32,
    out_result: *mut TokenizerEncodeResult,
) -> i32 {
    unsafe {
        let mut input_data = Vec::<&str>::with_capacity(num_seqs);
        for i in 0..num_seqs {
            let s = match std::str::from_utf8(std::slice::from_raw_parts(
                *input_cstr.offset(i as isize),
                *input_len.offset(i as isize),
            )) {
                Ok(s) => s,
                Err(e) => {
                    (*out_result).token_ids = std::ptr::null_mut();
                    (*out_result).len = 0;
                    set_last_error(format!("Invalid UTF-8 input: {}", e));
                    return 1;
                }
            };
            input_data.push(s);
        }
        let encoded_batch = match (*handle).encode_batch(input_data, add_special_tokens != 0) {
            Ok(r) => r,
            Err(e) => {
                (*out_result).token_ids = std::ptr::null_mut();
                (*out_result).len = 0;
                set_last_error(format!("Failed to encode batch: {}", e));
                return 1;
            }
        };
        for (i, encoded) in encoded_batch.into_iter().enumerate() {
            let len = encoded.len();
            let result = TokenizerEncodeResult {
                token_ids: Box::into_raw(encoded.into_boxed_slice()) as *mut u32,
                len: len,
            };
            *out_result.offset(i as isize) = result;
        }
        0
    }
}

#[no_mangle]
extern "C" fn tokenizers_free_encode_results(results: *mut TokenizerEncodeResult, num_seqs: usize) {
    unsafe {
        let slice = std::slice::from_raw_parts_mut(results, num_seqs);
        for result in &mut *slice {
            drop(Box::from_raw(std::slice::from_raw_parts_mut(
                result.token_ids,
                result.len,
            )));
        }
    }
}

#[no_mangle]
extern "C" fn tokenizers_decode(
    handle: *mut TokenizerWrapper,
    input_ids: *const u32,
    len: usize,
    skip_special_tokens: i32,
    out_result: *mut TokenizerDecodeResult,
) -> i32 {
    unsafe {
        let input_data = std::slice::from_raw_parts(input_ids, len);
        match (*handle).decode(input_data, skip_special_tokens != 0) {
            Ok(v) => {
                (*out_result).len = v.len();
                (*out_result).chars = Box::into_raw(v.into_bytes().into_boxed_slice()) as *mut u8;
                0
            }
            Err(err) => {
                (*out_result).chars = std::ptr::null_mut();
                (*out_result).len = 0;
                set_last_error(format!("Failed to decode: {}", err));
                1
            }
        }
    }
}

#[no_mangle]
extern "C" fn tokenizers_decode_batch(
    handle: *mut TokenizerWrapper,
    input_sentences: *const *const u32,
    input_lens: *const usize,
    num_seqs: usize,
    skip_special_tokens: i32,
    out_result: *mut TokenizerDecodeResult,
) -> i32 {
    unsafe {
        let mut sentences = Vec::<&[u32]>::with_capacity(num_seqs);
        for i in 0..num_seqs {
            let ids = std::slice::from_raw_parts(
                *input_sentences.offset(i as isize),
                *input_lens.offset(i as isize),
            );
            sentences.push(ids);
        }
        let decode_batch = match (*handle).decode_batch(&sentences, skip_special_tokens != 0) {
            Ok(v) => v,
            Err(err) => {
                (*out_result).chars = std::ptr::null_mut();
                (*out_result).len = 0;
                set_last_error(format!("Failed to decode batch: {}", err));
                return 1;
            }
        };
        for (i, decoded) in decode_batch.into_iter().enumerate() {
            let len = decoded.len();
            let result = TokenizerDecodeResult {
                chars: Box::into_raw(decoded.into_bytes().into_boxed_slice()) as *mut u8,
                len: len,
            };
            *out_result.offset(i as isize) = result;
        }
        return 0;
    }
}

#[no_mangle]
extern "C" fn tokenizers_free_decode_results(
    sentences: *mut TokenizerDecodeResult,
    num_seqs: usize,
) {
    unsafe {
        let slice = std::slice::from_raw_parts_mut(sentences, num_seqs);
        for result in &mut *slice {
            drop(Box::from_raw(std::slice::from_raw_parts_mut(
                result.chars,
                result.len,
            )));
        }
    }
}

#[no_mangle]
extern "C" fn tokenizers_free(wrapper: *mut TokenizerWrapper) {
    unsafe {
        drop(Box::from_raw(wrapper));
    }
}

#[no_mangle]
extern "C" fn tokenizers_get_vocab_size(handle: *mut TokenizerWrapper, size: *mut usize) {
    unsafe {
        *size = (*handle).tokenizer.get_vocab_size(true);
    }
}

#[no_mangle]
extern "C" fn tokenizers_id_to_token(
    handle: *mut TokenizerWrapper,
    id: u32,
    out_result: *mut TokenizerDecodeResult,
) -> i32 {
    unsafe {
        let str = match (*handle).tokenizer.id_to_token(id) {
            Some(s) => s,
            None => {
                (*out_result).chars = std::ptr::null_mut();
                (*out_result).len = 0;
                set_last_error(format!("ID {} not found in vocabulary", id));
                return 1;
            }
        };
        (*out_result).len = str.len();
        (*out_result).chars = Box::into_raw(str.into_bytes().into_boxed_slice()) as *mut u8;
        0
    }
}

#[no_mangle]
extern "C" fn tokenizers_token_to_id(
    handle: *mut TokenizerWrapper,
    token: *const u8,
    len: usize,
    out_id: *mut u32,
) -> i32 {
    unsafe {
        let token: &str = &String::from_utf8_lossy(std::slice::from_raw_parts(token, len));
        let id = (*handle).tokenizer.token_to_id(token);
        *out_id = match id {
            Some(id) => id,
            None => {
                set_last_error(format!("Token '{}' not found in vocabulary", token));
                return 1;
            }
        };
        0
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use err::LAST_ERROR;

    const FILEPATH: &str = "./bert-base-uncased-tokenizer.json";

    #[test]
    fn tokenizers_new_from_file_test() {
        let handle = tokenizers_new_from_file(FILEPATH.as_ptr(), FILEPATH.len());
        let msg = LAST_ERROR.take();
        println!("LAST_ERROR: {:?}", msg);
        assert!(msg.is_none());
        assert!(!handle.is_null());
        tokenizers_free(handle);
    }

    #[test]
    fn tokenizers_new_from_str_test() {
        let json_str = std::fs::read_to_string(FILEPATH).unwrap();
        let handle = tokenizers_new_from_str(json_str.as_ptr(), json_str.len());
        let msg = LAST_ERROR.take();
        println!("LAST_ERROR: {:?}", msg);
        assert!(msg.is_none());
        assert!(!handle.is_null());
        tokenizers_free(handle);
    }

    #[test]
    fn tokenizers_encode_test() {
        let handle = tokenizers_new_from_file(FILEPATH.as_ptr(), FILEPATH.len());
        let mut out_result = TokenizerEncodeResult {
            token_ids: std::ptr::null_mut(),
            len: 0,
        };
        let input = "Hello, world!";
        let ret = tokenizers_encode(
            handle,
            input.as_ptr(),
            input.len(),
            0,
            &mut out_result as *mut TokenizerEncodeResult,
        );
        let msg = LAST_ERROR.take();
        println!("LAST_ERROR: {:?}", msg);
        println!("Encoded IDs: {:?}", unsafe {
            std::slice::from_raw_parts(out_result.token_ids, out_result.len)
        });
        assert!(msg.is_none());
        assert_eq!(ret, 0);
        let expected_ids = [7592, 1010, 2088, 999];
        assert_eq!(
            unsafe { std::slice::from_raw_parts(out_result.token_ids, out_result.len) },
            &expected_ids
        );
        assert_eq!(out_result.len, expected_ids.len());
        tokenizers_free_encode_results(Box::into_raw(Box::new(out_result)), 1);
        tokenizers_free(handle);
    }

    #[test]
    fn tokenizers_decode_test() {
        let handle = tokenizers_new_from_file(FILEPATH.as_ptr(), FILEPATH.len());
        let input_ids = [7592, 1010, 2088, 999];
        let mut out_result = TokenizerDecodeResult {
            chars: std::ptr::null_mut(),
            len: 0,
        };
        let ret = tokenizers_decode(
            handle,
            input_ids.as_ptr(),
            input_ids.len(),
            0,
            &mut out_result as *mut TokenizerDecodeResult,
        );
        let msg = LAST_ERROR.take();
        println!("LAST_ERROR: {:?}", msg);
        let decoded_str = unsafe {
            String::from_utf8_lossy(std::slice::from_raw_parts(out_result.chars, out_result.len))
        };
        println!("Decoded string: {}", decoded_str);
        assert!(msg.is_none());
        assert_eq!(ret, 0);
        assert_eq!(decoded_str, "hello, world!"); // uncased
        tokenizers_free_decode_results(Box::into_raw(Box::new(out_result)), 1);
        tokenizers_free(handle);
    }

    #[test]
    fn tokenizers_encode_batch_test() {
        let handle = tokenizers_new_from_file(FILEPATH.as_ptr(), FILEPATH.len());
        let inputs = vec!["Hello, world!", "Tokenizers are great."];
        let mut input_cstrs: Vec<*const u8> = Vec::new();
        let mut input_lens: Vec<usize> = Vec::new();
        for input in &inputs {
            input_cstrs.push(input.as_ptr());
            input_lens.push(input.len());
        }
        let num_seqs = inputs.len();
        let mut out_results = [
            TokenizerEncodeResult {
                token_ids: std::ptr::null_mut(),
                len: 0,
            },
            TokenizerEncodeResult {
                token_ids: std::ptr::null_mut(),
                len: 0,
            },
        ];
        let ret = tokenizers_encode_batch(
            handle,
            input_cstrs.as_ptr(),
            input_lens.as_ptr(),
            num_seqs,
            0,
            out_results.as_mut_ptr(),
        );
        let msg = LAST_ERROR.take();
        println!("LAST_ERROR: {:?}", msg);
        assert!(msg.is_none());
        assert_eq!(ret, 0);
        let expected = [
            vec![7592, 1010, 2088, 999],
            vec![19204, 17629, 2015, 2024, 2307, 1012],
        ];
        for (i, result) in out_results.iter().enumerate() {
            let encoded_ids = unsafe { std::slice::from_raw_parts(result.token_ids, result.len) };
            println!("Input {}: Encoded IDs: {:?}", i, encoded_ids);
            assert_eq!(encoded_ids, &expected[i]);
        }
        tokenizers_free_encode_results(out_results.as_mut_ptr(), num_seqs);
        tokenizers_free(handle);
    }

    #[test]
    fn tokenizers_decode_batch_test() {
        let handle = tokenizers_new_from_file(FILEPATH.as_ptr(), FILEPATH.len());
        let input_ids = [
            vec![7592, 1010, 2088, 999],
            vec![19204, 17629, 2015, 2024, 2307, 1012],
        ];
        let mut input_cstrs: Vec<*const u32> = Vec::new();
        let mut input_lens: Vec<usize> = Vec::new();
        for ids in &input_ids {
            input_cstrs.push(ids.as_ptr());
            input_lens.push(ids.len());
        }
        let num_seqs = input_ids.len();
        let mut out_results = [
            TokenizerDecodeResult {
                chars: std::ptr::null_mut(),
                len: 0,
            },
            TokenizerDecodeResult {
                chars: std::ptr::null_mut(),
                len: 0,
            },
        ];
        let ret = tokenizers_decode_batch(
            handle,
            input_cstrs.as_ptr(),
            input_lens.as_ptr(),
            num_seqs,
            0,
            out_results.as_mut_ptr(),
        );
        let msg = LAST_ERROR.take();
        println!("LAST_ERROR: {:?}", msg);
        assert!(msg.is_none());
        assert_eq!(ret, 0);
        let expected = ["hello, world!", "tokenizers are great."];
        for (i, result) in out_results.iter().enumerate() {
            let decoded_str = unsafe {
                String::from_utf8_lossy(std::slice::from_raw_parts(result.chars, result.len))
            };
            println!("Input {}: Decoded string: {}", i, decoded_str);
            assert_eq!(decoded_str, expected[i]);
        }
        tokenizers_free_decode_results(out_results.as_mut_ptr(), num_seqs);
        tokenizers_free(handle);
    }

    #[test]
    fn tokenizers_id_to_token_test() {
        let handle = tokenizers_new_from_file(FILEPATH.as_ptr(), FILEPATH.len());
        let mut out_result = TokenizerDecodeResult {
            chars: std::ptr::null_mut(),
            len: 0,
        };
        let id = 7592; // "hello"
        let ret = tokenizers_id_to_token(handle, id, &mut out_result as *mut TokenizerDecodeResult);
        let msg = LAST_ERROR.take();
        println!("LAST_ERROR: {:?}", msg);
        assert!(msg.is_none());
        assert_eq!(ret, 0);
        let token_str = unsafe {
            String::from_utf8_lossy(std::slice::from_raw_parts(out_result.chars, out_result.len))
        };
        println!("Token for ID {}: {}", id, token_str);
        assert_eq!(token_str, "hello");
        tokenizers_free_decode_results(Box::into_raw(Box::new(out_result)), 1);
        tokenizers_free(handle);
    }

    #[test]
    fn tokenizers_token_to_id_test() {
        let handle = tokenizers_new_from_file(FILEPATH.as_ptr(), FILEPATH.len());
        let token = "hello";
        let mut out_id: u32 = 0;
        let ret =
            tokenizers_token_to_id(handle, token.as_ptr(), token.len(), &mut out_id as *mut u32);
        let msg = LAST_ERROR.take();
        println!("LAST_ERROR: {:?}", msg);
        assert!(msg.is_none());
        assert_eq!(ret, 0);
        println!("ID for token '{}': {}", token, out_id);
        assert_eq!(out_id, 7592);
        tokenizers_free(handle);
    }
}
