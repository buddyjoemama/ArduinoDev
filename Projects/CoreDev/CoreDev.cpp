/*
 * CoreDev.cpp
 *
 * Created: 12/21/2012 8:46:09 PM
 *  Author: Brian
 */ 
#include "Arduino.h"

int led = 13;

// the setup routine runs once when you press reset:
void setup() {
	// initialize the digital pin as an output.
	pinMode(led, OUTPUT);
}

// the loop routine runs over and over again forever:
void loop() {
	digitalWrite(led, HIGH);   // turn the LED on (HIGH is the voltage level)
	delay(2000);               // wait for a second
	digitalWrite(led, LOW);    // turn the LED off by making the voltage LOW
	delay(2000);               // wait for a second
}