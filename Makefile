#
# Makefile for PicForth
#

COMPILER=	picforth.fs
LIBRARIES=	picisr.fs libfetch.fs libstore.fs picflash.fs piceeprom.fs \
		librshift.fs liblshift.fs multitasker.fs priotasker.fs \
		libnibble.fs libcmove.fs libstrings.fs

PROGS=		booster.hex dcc2.hex generator.hex silver.hex \
		spifcard.hex taskexample.hex controller.hex i2cloader.hex

GFORTH?=	gforth

all:	${PROGS:.hex=.disasm} CHANGES.html docs
docs:
	cd doc && ${MAKE}
.PHONY:	all docs

.SUFFIXES: .fs .hex .asm .disasm .dump .serprog

interactive:
	${GFORTH} picforth.fs -e 'host picquit'

.fs.hex: ${COMPILER} ${LIBRARIES}
	${GFORTH} picforth.fs -e 'include $< file-dump $@ map bye' | \
		sort -o ${<:fs=map}

.fs.disasm: ${COMPILER} ${LIBRARIES}
	${MAKE} ${<:fs=hex}
	${GFORTH} picforth.fs -e 'include $< dis bye' > $@

.hex.asm:
	gpdasm $< > $@

.disasm.dump:
	less $<

.fs.serprog: ${COMPILER} ${LIBRARIES} serial.fs
	${MAKE} ${<:fs=hex}
	${GFORTH} picforth.fs -e 'include $< include serial.fs serprog bye'
#	${GFORTH} picforth.fs -e 'include $< include serial.fs serprog firmware bye'

RELEASEVERSION = 0.30

release:
	${MAKE} all
	mkdir picforth-${RELEASEVERSION}
	install -m 644 `cat MANIFEST` picforth-${RELEASEVERSION}
	tar zcvf picforth-${RELEASEVERSION}.tar.gz picforth-${RELEASEVERSION}
	rm -rf picforth-${RELEASEVERSION}
	chmod a+r picforth-${RELEASEVERSION}.tar.gz

CHANGES.html: CHANGES makedoc.pl
	perl makedoc.pl < CHANGES > CHANGES.html

WWWDIR=/home/sam/build/rfc1149/devel

installwww:
	${MAKE} release
	${MAKE} installwww2

installwww2:
	cp booster.fs booster.disasm ${WWWDIR}
	cp CHANGES.html ${WWWDIR}/CHANGES.picforth.html
	cp doc/picforth.html doc/picforth.pdf ${WWWDIR}/doc
	scp -p picforth-${RELEASEVERSION}.tar.gz \
		marvin:/home/rfc1149.data/download/picforth/
	echo "Do not forget to edit ${WWWDIR}/picforth.whtml"

taskexample.hex: multitasker.fs
taskexample.disasm: multitasker.fs

clean::
	rm -f *.hex *.map *.disasm CHANGES.html *~
	cd doc && ${MAKE} clean
