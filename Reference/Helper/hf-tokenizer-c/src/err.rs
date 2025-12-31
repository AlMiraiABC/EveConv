
thread_local! {
    pub(crate) static LAST_ERROR: std::cell::RefCell<Option<String>> = std::cell::RefCell::new(None);
}

pub fn set_last_error(err: String) {
    LAST_ERROR.with(|prev| {
        *prev.borrow_mut() = Some(err);
    });
}

#[no_mangle]
pub extern "C" fn tokenizers_get_last_error(out_cstr: *mut *mut u8, out_len: *mut usize) {
    LAST_ERROR.with(|prev| {
        if let Some(err) = &*prev.borrow() {
            unsafe {
                *out_cstr = err.as_ptr() as *mut u8;
                *out_len = err.len();
            }
        } else {
            unsafe {
                *out_cstr = std::ptr::null_mut();
                *out_len = 0;
            }
        }
    });
}
