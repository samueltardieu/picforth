\ -----------------------------------------------------------------
\ libi2c.fs
\ 
\ revised version 0.2
\   - now leaner/cleaner, and supports sequential read/write
\ 
\ PicForth words supporting PIC Master-mode I2C access
\ to serial EEPROMs (such as 24XXnnn)
\ via the on-chip MSSP module (16F87X etc)
\ 
\ The main user-level words offered by this module are:
\ 
\   i2c.init  ( devaddr -- )
\     - initialises i2c interface, in respect of I2C device
\       address 'devaddr' (a number from 0 to 7, corresponding
\       with the levels on A0-A2 on the I2C chip in question)
\ 
\   i2c@      ( addrL addrH -- byt )
\     - reads a single byte from I2C device from address addrH:addrL
\ 
\   i2c!      ( byt addrL addrH )
\     - writes a single byte to I2C device to address addrH:addrL )
\ 
\ Sequential read-write operations are supported by the following words:
\ 
\   i2c.read-begin ( addrL addrH status )
\     - starts a sequential read operation, returning zero status if ok
\ 
\   i2c.read-byte  ( -- byte )
\     - reads next byte in a sequence
\ 
\   i2c.read-end   ( -- )
\     - finishes a sequential read operation
\ 
\   i2c.write-begin ( addrL addrH status )
\     - starts a sequential write operation, returning zero status if ok
\ 
\   i2c.write-byte  ( byte -- )
\     - writes next byte in a sequence
\ 
\   i2c.write-end   ( -- )
\     - finishes a sequential write operation
\ 
\ Written by David McNab <david@freenet.org.nz>
\ Copyright (c) November 2004
\ Released under the GNU Library Public License
\ 
\ Notes:
\   - requires PIC with an MSSP module, eg PIC16F87x[A]
\   - uses only the MSSP module, talking via the hardwired PortC pins 3 and 4
\   - uses PIC-as-master mode
\   - always uses 2-byte addresses, so is only suitable for memory devices with
\     2-byte addresses, including:
\      - 24XX512, 24XX256, 24XX128, 24XX64, 24XX32, 24XX16, 24XX08, 24XX04
\     (who the heck wants to use smaller devices anyway, when these ones
\     are so cheap?!?)
\   - when doing writes - single-byte and sequential - you MUST wait 5ms
\     for the 24xxnnn to complete the operation
\   - sequential write operations are limited to 64-byte pages
\ 
\ References:
\   - Microchip PIC16F87X[A] data sheet
\       - Chapter 9, MSSP module, particularly section 9.4, I2C Mode
\   - Microchip 24XXnnn CMOS Serial EEPROM datasheets

\ needs libtty.fs
meta
\ -------------------------------------------------
\ Start user configuration - change these as needed

\ set these as needed
\ PIC clock frequency
&20000000 constant fosc-hz

\ desired I2C baudrate
&400000   constant i2c.clock
\  &100000   constant i2c.clock

\ End user configuration
\ ----------------------------------

macro

: ?dup dup if dup then ;

target

\ pin assignments
\ 
\ Don't try to change these, since
\ they're internally hardwired to the PIC MSSP module

3 pin-c i2c.SCL  \ clock
4 pin-c i2c.SDA  \ data

\ calculate clock prescaler, make it available in target space

meta fosc-hz i2c.clock / 4 / 1 - target constant i2c.clockval

\ failure codes

$01 constant i2c.err.read-addrH
$02 constant i2c.err.read-addrL
$03 constant i2c.err.read-ctrlin

$04 constant i2c.err.write-addrH
$05 constant i2c.err.write-addrL
$06 constant i2c.err.write-data

\ module variables

variable i2c.ctrlbyte   \ cached control in/out value
variable i2c.tmp          \ temporary scratch byte

code i2c.bank0
    reg-rp0     bcf
    reg-rp1     bcf
                return
end-code
code i2c.bank1
    reg-rp0     bsf
    reg-rp1     bcf
                return
end-code

\ wait for operation to complete

: i2c.waitmssp      ( -- )

    i2c.bank0

    begin
        sspif bit-set?
    until
    sspif bit-clr
;

\ i2c.tx-byte
\ 
\ low-level send of a byte to I2C

: i2c.tx-byte ( byt -- )

    i2c.bank0

    sspbuf !
    i2c.waitmssp            \ wait for completion
\    ackstat bit-set?        \ check for ack/nack
;

\ i2c.rx-byte
\ 
\ Receive a char via I2C

: i2c.rx-byte  ( -- byte )

    i2c.bank1

    \ Wait for Transmit to end 
    begin r/w bit-clr? until    \ while ( SSPSTAT & R_W_MASK  );
  
    rcen bit-set                \ set_bit( SSPCON2, RCEN );      // Enable I2C receiver 
    i2c.waitmssp                \ I2CWait();                     // Wait for data to arrive. 

    sspbuf @                    \ return SSPBUF;                 // Return the data 
;

\ i2c.tx-ack
\ 
\ Send an I2C ACK

: i2c.tx-ack    ( -- )

    i2c.bank1

    ackdt bit-clr           \ clear_bit( SSPCON2, ACKDT );   // Setup for ACK 
    acken bit-set           \ set_bit( SSPCON2, ACKEN );     // Send ACK 

    i2c.waitmssp
;

\ i2c.tx-nack
\ 
\ Send an I2C NACK

: i2c.tx-nack    ( -- )

    i2c.bank1

    ackdt bit-set           \ set_bit( SSPCON2, ACKDT );   // Setup for NACK 
    acken bit-set           \ set_bit( SSPCON2, ACKEN );     // Send NACK 
    i2c.waitmssp
;

\ i2c.tx-start
\ 
\ Generate an I2C START

: i2c.tx-start      ( -- )

    i2c.bank1

    sen bit-set         \ set_bit( SSPCON2, SEN );       // Initiate START condition 

    i2c.waitmssp        \ I2CWait();                     // Wait for completion 
;

\ i2c.tx-restart
\ 
\ Generate an I2C RESTART

: i2c.tx-restart      ( -- )

    i2c.bank1

    rsen bit-set         \ set_bit( SSPCON2, RSEN );       // Initiate RESTART condition 
    i2c.waitmssp        \ I2CWait();                     // Wait for completion 
;

\ i2c.tx-stop
\ 
\ Generate an I2C STOP

: i2c.tx-stop      ( -- )

    i2c.bank1

    pen bit-set         \ set_bit( SSPCON2, PEN );       // Initiate STOP condition 
    i2c.waitmssp        \ I2CWait();                     // Wait for completion 
;

\ i2c.tx-ctrl-in
\ 
\ Sends a CTRL IN byte to EEPROM device

: i2c.tx-ctrl-in        ( -- )

    i2c.ctrlbyte @ i2c.tx-byte
;

\ i2c.tx-ctrl-out
\ 
\ Sends a CTRL OUT byte to EEPROM device

: i2c.tx-ctrl-out        ( -- )

    i2c.ctrlbyte @ 1 or i2c.tx-byte
;

\ Configure the MSSP as an I2C Port for PIC16F87x

: i2c.init ( devaddr -- )

\ ttyl" i2c.init: entered"

    \ create template control byte
    $7 and rlf-tos $a0 or $fe and i2c.ctrlbyte !

    \ configure the required I2P PortC pins as inputs,
    \ so any writes to PortC don't interfere with the MSSP
    i2c.SCL >input
    i2c.SDA >input

    i2c.bank0

    \ set it up for I2C master mode
    sspm3 bit-set
    sspm2 bit-clr
    sspm1 bit-clr
    sspm0 bit-clr
    cke bit-clr

    i2c.bank1

    \ reset mssp states
    0 sspcon2 !

    \ set baud rate, as configured
    smp bit-set             \ fast clock, so need smp
    i2c.clockval sspadd !

    \ turn on MSSP
    sspen bit-set

\ ttyl" i2c.init: done"

;

 \ i2c.tx-2byte-addr
\ 
\ loads a 2-byte address into i2c device

: i2c.tx-2byte-addr     ( -- status )

\ tty" i2c.tx-2byte-addr: sending RESTART" .s

    i2c.tx-restart

    \ send CTRL IN
\ tty" i2c.tx-2byte-addr: sending CTRL IN" .s

    i2c.tx-ctrl-in          ( addrL addrH )

\ tty" i2c.tx-2byte-addr: sending addrH" .s

    \ send address MSB
    i2c.tx-byte             ( addrL )

\ tty" i2c.tx-2byte-addr: sending addrL" .s

    \ send address LSB
    i2c.tx-byte             (  )

    0
;

\ i2c.read-begin
\ 
\ Sends ctrl in, addrL, addrH then ctrl out
\ returns zero if successful, or an error code if failed

: i2c.read-begin        ( addrL addrH -- status )

    \ load the address
    i2c.tx-2byte-addr ?dup if exit then

\ tty" i2c.read-begin: sending RESTART" .s

    \ need a restart
    i2c.tx-restart

\ tty" i2c.read-begin: sending CTRL OUT" .s

    \ send CTRL OUT
    i2c.tx-ctrl-out         (  )

\ tty" i2c.read-begin: done" .s

    \ done
    0
;

\ i2c.read-byte
\ 
\ read the nth byte during a sequential read, and issue
\ an ACK to allow more bytes

: i2c.read-byte         ( -- byte )

    i2c.rx-byte
    i2c.tx-ack
;

\ i2c.read-end
\ 
\ terminates a read sequence

: i2c.read-end      ( -- )

    i2c.rx-byte drop
    i2c.tx-nack
    i2c.tx-stop
;

\ i2c.write-begin
\ 
\ start a sequential write

: i2c.write-begin       ( addrL addrH -- status )

    i2c.tx-2byte-addr
;

\ i2c.write-byte
\ 
\ writes a byte in sequence

: i2c.write-byte        ( byte -- )

    i2c.tx-byte
;

\ i2c.write-end
\ 
\ terminates a write sequence

: i2c.write-end

    i2c.tx-stop
;

\ i2c@
\ 
\ Single random read
\ 
\ Returns byte and zero if successful, or error code if failed

: i2c@      ( addrL addrH -- [byte] status )

\ tty" i2c@: calling read-begin" .s

    i2c.read-begin ?dup if exit then

\ tty" i2c@: calling read-byte" .s

    i2c.read-byte

\ tty" i2c@: calling read-end" .s

    i2c.read-end
    0

\ tty" i2c@: done" .s

;

\ i2c!
\ 
\ Stores a single byte at an arbitrary address

: i2c!          ( byte addrL addrH -- status )

\ ttyl" i2c!: sending address"

    i2c.write-begin ?dup if exit then

    \ ok, send the single byte
    i2c.write-byte
    
    \ done
    i2c.write-end
    
    0
;


