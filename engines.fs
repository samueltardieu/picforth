include piceeprom.fs

variable mul-acc-high variable mul-acc-low
variable mul-fac 1
variable mul-res-high variable mul-res-low

\ Multiply a 16 bits value in mul-acc by a 8 bits value in mul-fac -
\ the result must fit in 16 bits and is stored in mul-res. This is a
\ destructive operation for both operands.

: mult ( -- )
    0 mul-res-high ! 0 mul-res-low !
    begin
	\ While multiplicand is not 0
	mul-fac @ 0= if exit then
	\ Shift multiplicand right and act if low bit was set
	c bit-clr mul-fac rrf!
	c bit-set? if
	    \ Add low byte of accumulator to result
	    mul-acc-low @ mul-res-low +! c bit-set? if 1 mul-res-high +! then
	    \ Add high byte of accumulator to result
	    mul-acc-high @ mul-res-high +!
	then
	\ Shift accumulator left
	c bit-clr mul-acc-low rlf! mul-acc-high rlf!
    again
;

variable position1 variable position2 variable position3 variable position4
variable ticks1 variable ticks2

\ Reset ticks counter

: reset-ticks ( -- ) 0 ticks1 ! 0 ticks2 ! ;

\ Add and remove one tick

: inc-ticks ( -- ) 1 ticks2 +! z bit-set? if 1 ticks1 +! then ;
: dec-ticks ( -- ) 1 >w ticks2 w-! c bit-set? if 1 ticks1 -! then ;

\ Add 16 bits ticks to current 32bits position. This also resets tick
\ counter.

: update-position ( -- )
    ticks2 @ position4 +! c bit-set? if 1 ticks1 +! then
    ticks1 @ position3 +! c bit-set? if
	1 position2 +! c bit-set? if
	    1 position1 +!
	then
    then
    reset-ticks
;

\ Add acceleration (32 bits) to velocity (32 bits). Also, limit velocity
\ to the limit (32 bits) value. Returns a non-null value if velocity has
\ been limited.

variable accel1 variable accel2 variable accel3 variable accel4
variable vel1 variable vel2 variable vel3 variable vel4
variable limit1 variable limit2 variable limit3 variable limit4

: copy-limit-4 ( -- true ) limit4 @ vel4 ! 1 ;
: copy-limit-3 ( -- true ) limit3 @ vel3 ! copy-limit-4 ;
: copy-limit-2 ( -- true ) limit2 @ vel2 ! copy-limit-3 ;
: copy-limit-1 ( -- true ) limit1 @ vel1 ! copy-limit-2 ;

: limit-velocity-+ ( -- limited? )
    vel1 @ limit1 @ < if 0 exit then
    limit1 @ vel1 @ < if copy-limit-1 exit then
    vel2 @ limit2 @ < if 0 exit then
    limit2 @ vel2 @ < if copy-limit-2 exit then
    vel3 @ limit3 @ < if 0 exit then
    limit3 @ vel3 @ < if copy-limit-3 exit then
    vel4 @ limit4 @ < if 0 exit then
    limit4 @ vel4 @ < if copy-limit-4 exit then
    1
;
    
: update-velocity-+ ( -- limited? )
    vel4 @ accel4 +! z bit-set? if
	1 vel3 +! z bit-set? if
	    1 vel2 +! z bit-set? if
		1 vel1 +!
	    then
	then
    then
    vel3 @ accel3 +! z bit-set? if
	1 vel2 +! z bit-set? if
	    1 vel1 +!
	then
    then
    vel2 @ accel2 +! z bit-set? if
	1 vel1 +!
    then
    limit-velocity-+
;

: limit-velocity-- ( -- limited? )
    limit1 @ vel1 @ < if 0 exit then
    vel1 @ limit1 @ < if copy-limit-1 exit then
    limit2 @ vel2 @ < if 0 exit then
    vel2 @ limit2 @ < if copy-limit-2 exit then
    limit3 @ vel3 @ < if 0 exit then
    vel3 @ limit3 @ < if copy-limit-3 exit then
    limit4 @ vel4 @ < if 0 exit then
    vel4 @ limit4 @ < if copy-limit-4 exit then
    1
;
    
: update-velocity-- ( -- limited ?)
    vel4 @ accel4 -! c bit-set? if
	1 >w vel3 w-! c bit-set? if
	    vel2 w-! c bit-set? if
		1 vel1 -!
	    then
	then
    then
    vel3 @ accel3 -! c bit-set? if
	1 >w vel2 w-! c bit-set? if
	    1 vel1 -!
	then
    then
    vel2 @ accel2 -! c bit-set? if
	1 vel1 -!
    then
    limit-velocity--
;

\ Coding wheels transitions. The edges are memorized, sensors are called A and B.
\ A low edge is 00, A high edge is 01, B low edge is 10, B high edge is 11.
\ In the following array, 1 means forward, -1 backward, and 0 reverse. We put a
\ 2 to indicate an impossible value (two successive identical transitions), which can
\ happen if the robot stops right at a transition.
\ The index is a four bits value: 2 (high) bits with the previous transition and
\ 2 (low) bits with the latest transition.
\
\     01 _____ 00 01 _____ 00
\  _____|     |_____|     |______           A sensor
\ ___ 10 11 _____ 10 11 _____ 10
\    |_____|     |_____|     |______        B sensor

eetable transitions
  table> ( 00 00 ) 2  ( 00 01 ) 0  ( 00 10 ) 1  ( 00 11 ) -1
  table> ( 01 00 ) 0  ( 01 01 ) 2  ( 01 10 ) -1 ( 01 11 ) 1
  table> ( 10 00 ) -1 ( 10 01 ) 1  ( 10 10 ) 2  ( 10 11 ) 0
  table> ( 11 00 ) 1  ( 11 01 ) -1 ( 11 10 ) 0  ( 11 11 ) 2
end-table

variable previous-transitions

\ Given a transition, return the current direction: 1 (forward), -1 (backward),
\ 0 (reverse) or 2 (impossible: probably a glitch, should be ignored).

: direction ( transition -- )
  previous-transitions @ 2* 2* + $f and dup previous-transitions ! transitions ;

variable current-direction

: handle-tick ( -- )
  current-direction @ 0< if dec-ticks exit then inc-ticks ;

: handle-direction ( direction -- )
  dup 0= if                         \ Reverse direction
    drop current-direction @ negate current-direction ! handle-tick exit
  then
  dup 2 = if                        \ Invalid move
    drop exit
  then
  current-direction !  handle-tick
;

: handle-event ( transition -- ) direction handle-direction ;

: init ( -- )
  reset-ticks
  1 current-direction !
  0 previous-transitions !
;
