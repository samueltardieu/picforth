\
\ PicForth library file
\
\ This library file has been written by Samuel Tardieu <sam@rfc1149.net>.
\ It belongs to the public domain. Do whatever you want with it.
\

\ ----------------------------------------------------------------------
\ Priority-based cooperative multitasker (state-machine based)
\ ----------------------------------------------------------------------

host

\ Task structure:
\    - address of task semaphore address (1 cell)  (or 0 for a bit task)
\    - address of task entry point       (1 cell)
\    - task priority                     (1 cell)
\    - previous task                     (1 cell)
\    - task byte                         (1 cell)  (if task semaphore is 0)
\           (byte is inverted is condition is inverted)
\   or bit number to check for run       (1 cell)
\    - task bit                          (1 cell)  (if task semaphore is 0)

variable last-task
variable prio
variable mtorig
variable task-count
variable last-opt
variable last-byte
variable last-bit

255 constant idleprio

: allocate-bit ( -- )
    last-bit @ if -1 last-bit +! exit then
    data-here dup last-byte ! 1+ data-org 7 last-bit !
;

: new-task ( -- previous )
    1 task-count +!
    align here last-task @ swap last-task !
;

variable w-set?

: tasks-init ( -- )
    0 last-byte !
    last-task @
    begin dup while dup @ ?dup if
	dup 0< if
	    abs clrf
	else
	    dup last-byte @ <> if
		last-byte @ 0= if ff movlw then
		dup last-byte ! movwf
	    else
		drop
	    then
	then
    then 3 cells + @ repeat drop
;

' tasks-init add-to-init-chain

variable jump-address

: act-last ( -- )
    task-count @ current-bank @ or if
	meta> ahead
	jump-address !
    else
	mtorig @ goto
	0 jump-address !
    then
    reachable
;

: restore-last ( -- )
    jump-address @ ?dup if
	meta> then
    then
;

: sameprio? ( addr -- flag )
    begin
	3 cells + @ dup while
	dup 2 cells + @ prio @ = if drop true exit then
    repeat
    drop false ;

: task-prio ( n -- )
    prio !
    last-task @
    begin dup while
	dup 2 cells + @ prio @ = if
	    -1 task-count +!
	    prio @ idleprio = if
		dup cell+ @ call
	    else
		dup @ if
		    \ Semaphore test
		    dup @ abs adjust-bank over 4 cells + @ btfsc act-last
		    current-bank @ >r restore-bank
		    dup @ 0< if dup @ ,f decf then dup cell+ @ call
		else
		    \ Bit test
		    dup 5 cells + @ over  4 cells + @ dup abs adjust-bank
		    swap 0< if
			swap btfsc
		    else
			swap btfss
		    then
		    act-last
		    current-bank @ >r restore-bank
		    dup cell+ @ call
		then
		dup sameprio? 0= if mtorig @ goto then
		restore-last
		r> current-bank ! restore-bank
	    then
	then
	3 cells + @
    repeat
;

: tasks-schedule ( n -- )
    tcshere mtorig !
    clrwdt
    256 0 do i task-prio loop
    \ If the last test was optimized, no need to add an extra goto data-here
    jump-address @ if mtorig @ goto then
    unreachable ;

: (task) ( prio addr -- )
    new-task >r
    , tcshere , , r> ,
;

meta

: task ( prio "name" -- )
    create allocate-bit last-byte @ tuck (task) 1+ data-org last-bit @ , ;
: task-cond ( prio "name" -- ) create data-here tuck negate (task) 1+ data-org ;
: task-idle ( -- ) idleprio 0 (task) ;
: task-set ( port bit prio -- ) 0 (task) swap , , ;
: task-clear ( port bit prio -- ) 2>r invert 2r> task-set ;

: start ( addr -- )
    dup @ (literal) 4 cells + @ (literal)
    meta> bit-set
;

: stop ( addr -- )
    dup @ (literal) 4 cells + @ (literal)
    meta> bit-clr
;

: signal ( addr -- )
    1 (literal) @ abs (literal)
    meta> +!
;

: multitasker ( -- )
    tasks-schedule
;

target
