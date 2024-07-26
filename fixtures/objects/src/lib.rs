/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

use crossbeam::channel::{Receiver, Sender};
use once_cell::sync::Lazy;
use std::sync::{Arc, Mutex};

static RECEIVER_COUNT: Lazy<Mutex<i32>> = Lazy::new(|| Mutex::new(0));

uniffi::setup_scaffolding!();


#[derive(uniffi::Object)]
pub struct SignalReceiver {
    sender: Sender<()>,
    receiver: Receiver<()>,
}

#[uniffi::export]
impl SignalReceiver {
    pub fn receive_signal(&self) {
        self.sender.send(()).unwrap();
        self.receiver.recv().unwrap();
        self.sender.send(()).unwrap();
    }

    pub fn heart_beat(&self) -> String {
        "whoosh".to_string()
    }
}

impl Drop for SignalReceiver {
    fn drop(&mut self) {
        *RECEIVER_COUNT.lock().unwrap() -= 1;
    }
}

#[derive(uniffi::Object)]
pub struct SignalSender {
    sender: Sender<()>,
    receiver: Receiver<()>,
}

#[uniffi::export]
impl SignalSender {
    pub fn send_signal(&self) {
        self.sender.send(()).unwrap();
    }

    pub fn wait_for_receiver_to_appear(&self) {
        self.receiver.recv().unwrap();
    }

    pub fn wait_for_receiver_to_disappear(&self) {
        self.receiver.recv().unwrap();
    }
}

#[derive(uniffi::Record)]
pub struct Channel {
    pub sender: Arc<SignalSender>,
    pub receiver: Arc<SignalReceiver>,
}

#[uniffi::export]
pub fn create_channel() -> Channel {
    let (tx1, rx1) = crossbeam::channel::unbounded::<()>();
    let (tx2, rx2) = crossbeam::channel::unbounded::<()>();

    let receiver = SignalReceiver {
        sender: tx2,
        receiver: rx1,
    };

    let sender = SignalSender {
        sender: tx1,
        receiver: rx2,
    };

    *RECEIVER_COUNT.lock().unwrap() += 1;

    Channel {
        sender: Arc::new(sender),
        receiver: Arc::new(receiver),
    }
}

#[uniffi::export]
fn get_live_receiver_count() -> i32 {
    *RECEIVER_COUNT.lock().unwrap()
}
