#! /usr/bin/env python

"""
PIC Hex File downloader utility using the PicForth monitor

written Oct 2004 by David McNab
"""

# serial device for talking to bootmon
defaultDev = "/dev/ttyS0"

# how fast we're talking - uncomment one
#defaultBaudrate = 9600
#defaultBaudrate = 19200
#defaultBaudrate = 28800
#defaultBaudrate = 33600
defaultBaudrate = 57600

# address at which to write startup vector - uncomment one
startVector = 0xe34   # suits PIC 16F873[A] and 16F88[A]
#startVector = 0x1e34  # suits PIC 16F876/7[A]

# address at which we've org'ed our program, change as needed
progOrg = 0x5

import sys, os, termios, tty, time

progname = sys.argv[0]

class Hexfile:
    """
    parses an Intel hex file
    """
    def __init__(self, path):

        f = file(path)

        self.rawrecs = {}

        for line in f.readlines():       # grab each line from the file
            self.parseLine(line)         # decompose into address:data

        # write in the program start vector
        progOrgH = progOrg >> 8
        self.rawrecs[startVector] = 0x3000 | progOrgH  # movlw HIGH(progOrg)
        self.rawrecs[startVector+1] = 0x8a             # movwf PCLATH
        self.rawrecs[startVector+2] = 0x2800 | progOrg # goto progOrg
        
        # fill in any 'blanks' - make sure that for every entry, there's
        # actually a block of 4 words (needed for 16F87xA)
        for addr in self.rawrecs.keys():
            # nuke out b0 and b1
            addr1 = (addr >> 2) << 2
            
            for i in range(0, 4):
                if not self.rawrecs.has_key(addr1+i):
                    #print "Adding empty word for addr 0x%x" % (addr1+i)
                    self.rawrecs[addr1+i] = 0

        # sort the keys
        self.addresses = self.rawrecs.keys()
        self.addresses.sort()
        
        # and create list of address,value pairs
        self.data = [(addr, self.rawrecs[addr]) for addr in self.addresses]

        # and convert this to lines with ASCII addresses and data, ready for download
        self.ascdata = [(("%04X" % addr), ("%04X" % val)) for (addr, val) in self.data]

    def __getitem__(self, item):
        return self.data[item]

    def __len__(self):
        return len(self.data)
        
    def parseLine(self, line):
        """
        parses an individual line of Intel Hex file
        """
        hexlen = line[1:3]        # number of data bytes on the line
        hexaddr = line[3:7]       # starting address of data on line
        hextype = line[7:9]       # 00 - data, 01 - eof
        hexdata = line[9:-2]      # data bytes (use words for the 14 bit PIC instructions)
        hexcrc = line[-2:]        # crc of line

        linelen = self.fromHex(hexlen)    # decompose line length
        lineaddr = self.fromHex(hexaddr)  # decompose starting address

        if hextype != '01':                 # if not eof
            for i in range(0, linelen, 2):    # grab each data word
                s = hexdata[i*2+2:i*2+4] + hexdata[i*2:i*2+2]
                val = self.fromHex(s)
                #print "%s = %s" % (s, val)
                #self[(lineaddr + i)/2] = hexdata[i*2:i*2+4]    # populate dict. with addr:data
                self.rawrecs[(lineaddr + i)/2] = val

    def fromHex(self, s):
        
        return int(s, 16)

    def keys(self):
        """
        Convenience wrapper - returns dict keys in order
        """
        keys = dict.keys(self)
        keys.sort()
        return keys

    def hexkeys(self):
        """
        For debugging, returns keys in order, as hex
        """
        return "[" + ", ".join([("%x" % k) for k in self.keys()]) + "]"

class Tty:
    """
    Wraps a serial link to PIC
    """
    def __init__(self, dev=defaultDev, speed=defaultBaudrate):
        
        try:
            speedToken = getattr(termios, "B%d" % speed)
        except:
            raise Exception("Invalid baudrate %s" % repr(speed))

        fd = os.open(dev, os.O_RDWR | os.O_NONBLOCK, 0777)
        attr = termios.tcgetattr(fd)
        attr[4] = attr[5] = speedToken
        termios.tcsetattr(
            fd,
            termios.TCSANOW,
            attr)
        tty.setraw(fd)
    
        self.fd = fd

    def read(self, n=1, blocking=False):
        while 1:
            try:
                c = os.read(self.fd, n)
            except OSError:
                c = ''
            if c or not blocking:
                return c
    
    def write(self, s):
        os.write(self.fd, s)

    def __del__(self):
        os.close(self.fd)

def download(hexfile, dev=defaultDev):
    """
    Attempts to download an intel hex file to a PIC over serial
    """
    # create Hexfile object if not already one
    if isinstance(hexfile, str):
        hexfile = Hexfile(hexfile)

    # open a serial link    
    print "Opening serial link..."
    t = Tty(dev)

    # see if we're talking to a PicForth monitor
    print "Attempting connection with PicForth monitor (press Ctrl-C to abort)..."
    connected = False
    try:
        while True:
            t.write(" ")
            c = t.read()
            if c == '':
                time.sleep(0.1)
                sys.stdout.write(".")
                sys.stdout.flush()
                continue

            #print "Connecting, bootmon sent %s" % repr(c)

            # connected, now drain rx
            connected = True
            while 1:
                time.sleep(0.1)
                c = t.read()
                if c == '':
                    break
                #print "Connecting, bootmon sent %s" % repr(c)
            break

    except KeyboardInterrupt:
        print "\nDownload aborted by user"
        return

    print "\nConnected sucessfully, now downloading..."
    
    if 0:
        print hexfile.rawrecs
        return

    # send down a series of 'F' commands
    i = 0

    # compute values for determining address legality
    vecRomStart = "%04X" % (startVector - 4)
    vecRomEnd = "%04X" % (startVector - 1)
    vecAsciiStart = "%04X" % startVector
    vecAsciiEnd = "%04X" % (startVector + 3)

    for addr, val in hexfile.ascdata:
        # vet address
        #if (addr < "0004") or (addr >= "0E40" and addr <= "0FFF"):
        if (addr < "0004") \
                or (addr >= vecRomStart and addr <= vecRomEnd) \
                or (addr > vecAsciiEnd):
            #print "Skipping invalid setting %s->%s" % (addr, val)
            continue
        else:
            #print "Want to set %s to %s" % (addr, val)
            cmd = "F%s%s" % (addr, val)
            #print "Send command: %s" % cmd
            #continue
            
            t.write("F%s%s" % (addr, val))

            # seek reply chars from bootmon
            for c in ['!', '>']:
                c1 = t.read(blocking=1)
                if c1 != c:
                    print "Bootmon replied with %s, wanted '!'" % repr(c1)
                    raise Exception("Download failure")

            # display user feedback
            i += 1
            if i % 16 == 0:
                if i % 256 == 0:
                    if i % 1024 == 0:
                        sys.stdout.write("%dk\n" % (i / 1024))
                    else:
                        sys.stdout.write("*")
                else:
                    sys.stdout.write(".")

                sys.stdout.flush()

    if i % 1024:
        sys.stdout.write('\n')
    print "Successfully transferred %d bytes" % i

    # now send command to run prog
    print "Launching application"
    t.write("X0005")
    
    print "Download successful"

def usage(ret=1):

    print "Usage: %s file.hex" % progname
    print "Attempts download of a HEX file to a PIC device, via the PigForth monitor"
    sys.exit(ret)

def main():
    
    # barf if no arg
    if len(sys.argv) < 2:
        print "Missing filename argument"
        usage()

    path = sys.argv[1]
    
    download(path)

if __name__ == '__main__':
    main()
