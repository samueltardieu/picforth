\
\ This code controls 6 inputs and 8 outputs using a serial port. The
\ device accepts commands at 9600bps with the format !xy where x and y
\ are hexadecimal digits (case does not matter). The device sends the
\ state of the input once every 5 seconds or when they change, using the
\ format !xy<cr>.
\
\ Inputs are on port A, outputs on port B. A led on port C0 blinks every 5
\ seconds so that humans trust that the system is running.
\

pic16f87x

include multitasker.fs
include libnibble.fs

0 pin-c led

macro

: transmit-byte ( byte -- )
    begin txif bit-clr? while yield repeat
    txreg !
;

: transmit-nibble ( nibble -- ) nibble>hex transmit-byte ;

target

variable input
variable send?

\ Send content of variable input
task : send-input ( -- )
    begin
	\ Wait until we are asked to send data
	begin send? @ 0= while yield repeat 0 send? !
	\ Transmit the frame !XX<cr>
	[char] ! transmit-byte
	input @ dup 4 rshift transmit-nibble 0f and transmit-nibble
	$d transmit-byte
    again
;

task : check-input ( -- )
    begin
	\ If input state has changed, record it and send it
	input @ porta @ dup input ! xor if 1 send? +! then
	yield
    again
;

variable post-count

\ Every 5 seconds, send the data
task : timer ( -- )
    begin
	\ Toggle led and send data
	led toggle 1 send? +!
	\ Wait for 152*256*128µs
	98 post-count v-for
	    begin yield t0if bit-set? until
	    t0if bit-clr
	v-next
    again
;

variable recv-state
variable recv-char

task : receive-command ( -- )
    begin
	begin rcif bit-clr? while yield repeat
	\ '!' indicates a command start
	rcreg @ dup [char] ! = if drop 1 recv-state ! recurse exit then
	\ Decode nibble
	hex>nibble
	\ If this is the first nibble, store it in position
	recv-state @ 1 = if
	    swapf-tos recv-char ! 1 recv-state +!
	\ If this is the second nibble, build the byte and set it on the port
        else recv-state @ 2 = if
	    recv-char @ or portb ! 0 recv-state !
	else
	    \ Other states are impossible
	    drop
	then then
    again
;

\ Initialization and main program

: init ( -- )
    $06 adcon1 !                 \ Disable A/C converter
    $ff trisa !                  \ All pins of porta as inputs
    0 trisb !                    \ All pints of portb as outputs
    0 portb !                    \ Initial state is everything low
    $90 rcsta !                  \ serial-enable 8bits-rx continuous-receive
    $24 txsta !                  \ asynchronous transmit high-speed generator
    $19 spbrg !                  \ 9600 bauds with BGRH high at 4MhZ
    $be trisc !                  \ Port C6 is TX, port C0 is control led
    $07 option_reg !             \ 128 prescaler
    0 recv-state !               \ Initial state
;

main : main ( -- ) init multitasker ;

\ Configuration

fosc-hs set-fosc
