\
\ PicForth library file
\
\ This library file has been written by Samuel Tardieu <sam@rfc1149.net>.
\ It belongs to the public domain. Do whatever you want with it.
\

meta

variable l-task

\ Yield chain

: l-yield ( -- addr ) l-task @ 2 cells + ;
: chain-yield ( addr -- ) align here swap , l-yield @ , l-yield ! ;
: patch-addr ( addr host-tcb -- )
    l-task ! l-yield @
    begin dup while 2dup @ dup tcs@ rot or swap tcs! cell+ @ repeat 2drop
;

\ Two bytes are necessary to save the task structure (FSR, PCL, PCLATH,
\ 8 bytes stack)
\ The TCB on the host contains:
\   - a pointer to the target structure (1 cell)
\   - a pointer to the previous task (1 cell)
\   - the latest address to patch for a yield (1 cell)
\   - XT to execute (1 cell)

: task ( -- )
    align here data-here dup , 0b + data-org l-task @ , l-task ! 0 , tcshere , ;

: resume-task ( host-tcb -- )
    @ dup ,w movf fsr movwf dup 2 + ,w movf pclath movwf 1+ ,w movf pcl movwf ;

target

variable multitasker-pclath

meta

: init-tasks ( -- )
    l-task @ begin
	dup while
	dup @ dup 0b + movlw movwf
	dup 3 cells + @
	dup (literal) over @ 1+ (literal) meta> !
	8 rshift (literal) dup @ 2 + (literal) meta> !
	\ dup 3 cells + @ (literal) dup @ 1+ (literal) meta> !
	\ dup 3 cells + @ 8 rshift (literal) dup @ 2 + (literal) meta> !
	cell+ @
    repeat drop
;

' init-tasks add-to-init-chain

: multitasker ( -- )
    tcshere cbank (literal) multitasker-pclath (literal) meta> !
    tcshere clrwdt l-task @ begin
	dup while
	dup resume-task tcshere over patch-addr
	dup @ 1+ movwf fsr ,w movf l-task @ @ movwf
	cell+ @
    repeat drop goto unreachable
;

: yield ( -- )
    tcshere 4 + 8 rshift (literal) l-task @ @ 2 + (literal) meta> !
    multitasker-pclath (literal) meta> @
    pclath (literal) meta> !
    tcshere 2 + movlw
    tcshere 0 goto chain-yield
;

target
