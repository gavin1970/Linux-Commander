# Welcome to Linux Commander for Windows!
Linux Commander is a Windows Console Application using SSH.NET and with the use of PSCP can publish and refresh sync code with a remote linux machine.
It has Ansible installation "install-ansible" and validation process with Ansible prompts within a remote Linux server.

## Ansible Work
To find out more about how to run ansible at home, check out this this page I setup for how I'm doing it.
http://www.chizl.com/ansible/

## Linux Commander Connecting to Linux
<img src="https://github.com/gavin1970/Linux-Commander/blob/master/Linux-Commander/imgs/LinuxCommander05.png" alt="Example 1"/>

## Linux Commander Help Screen

	            ###   ###   ###  #########  ###        ##########   ###
	          ###     ###   ###  #########  ###        ##########     ###
	        ###       ###   ###  ###        ###        ###    ###       ###
	      ###         #########  #########  ###        ##########         ###
	      ###         #########  #########  ###        ##########         ###
	        ###       ###   ###  ###        ###        ###              ###
	          ###     ###   ###  #########  #########  ###            ###
	            ###   ###   ###  #########  #########  ###          ###



	     Please note, all Linux commands are accepted.  These are shortcuts or
	      windows calls, translated into linux commands or a mixture of both.


-=[ INTERNAL COMMANDS ]=-

	  about:                        Sets [set-local] path back to temp direcotry.
	                                connected to.
	                                e.g. about
	  ---------------------------------------------------------------------------
	  clear-local:                  Sets [set-local] path back to temp direcotry.
	                                e.g. clear-local
	  ---------------------------------------------------------------------------
	  cls:                          Clear the screen.
	                                e.g. cls
	  ---------------------------------------------------------------------------
	  edit:                         Will pull file from Linux, open in local windows editor, then upload it back from
	                                where it was located.
	                                e.g. edit [REMOTE_FILE_NAME]
	  ---------------------------------------------------------------------------
	  edit-config:                  Edit Linux Commander configuration file and refresh configuration
	                                after editor is closed.
	                                e.g. edit-config
	  ---------------------------------------------------------------------------
	  exit:                         Close Linux Commander.
	                                e.g. exit
	  ---------------------------------------------------------------------------
	  get-file:                     Pulls a file from current remote directory and saves it to
	                                [set-local] directory.
	                                e.g. get-file [REMOTE_FILE_NAME]
	  ---------------------------------------------------------------------------
	  -help:                        Linux Help Screen.
	                                e.g. -help
	  ---------------------------------------------------------------------------
	  install-ansible:              Will install PIP, Python3, and latest version of Ansible on remote connected
	                                Linux Server.
	                                e.g. install-ansible
	  ---------------------------------------------------------------------------
	  legend:                       Display all the colors for the directory listing.
	                                e.g. legend
	  ---------------------------------------------------------------------------
	  local-dir:                    Display files in the [set-local] folder.
	                                e.g. local-dir
	  ---------------------------------------------------------------------------
	  new-host:                     Disconnect from current host and allows connection to another host.
	                                e.g. new-host
	  ---------------------------------------------------------------------------
	  permissions:                  Help for a better understanding on permissions for directory listing.
	                                e.g. permissions
	  ---------------------------------------------------------------------------
	  publish:                      Pushes all files and folders from your [set-local] directory and uploads
	                                them to your [set-remote] directory.
	                                e.g. publish
	  ---------------------------------------------------------------------------
	  recon:                        If you get kicked or disconnected from the server.
	                                Quickly reconnect with existing creds.
	                                If already connected, recon
	                                will be ignored.
	                                e.g. recon
	  ---------------------------------------------------------------------------
	  refresh:                      Pulls all files and folders from set-remote directory and places them in the
	                                set-local directory.
	                                e.g. refresh
	  ---------------------------------------------------------------------------
	  send-file:                    Takes a file from [set-local] and sends it to [set-root] directory.
	                                e.g. send-file [LOCAL_FILE_NAME]
	  ---------------------------------------------------------------------------
	  set-local:                    Set local path where all files are saved to or sent from.
	                                e.g. set-local [[DRIVE][LOCAL_PATH]]
	  ---------------------------------------------------------------------------
	  set-remote:                   Set root remote path where all files and folders are
	                                published to from [set-local]
	                                e.g. set-remote [/REMOTE_PATH]
	  ---------------------------------------------------------------------------
	  sets:                         Shows both set-local and set-remote.
	                                e.g. sets
	  ---------------------------------------------------------------------------
	  sync-date:                    Uses your current windows date/time and sets the linux server date/time to match.
	                                e.g. sync-date
	  ---------------------------------------------------------------------------
	  view:                         View translates to cat.  View is an application and
	                                since Linux Commander is a Virual UI, it's used
	                                for passing command and getting results only.
	                                e.g. view [LOCAL_FILENAME]
	                                e.g. view [[SUBDIR\][LOCAL_FILENAME]]
	  ---------------------------------------------------------------------------
	  whereami:                     Displays the server, port, and current directory.
	                                e.g. whereami
	  ---------------------------------------------------------------------------
	  whoami:                       Displays the user connected to the server.
	                                e.g. whoami
	  ---------------------------------------------------------------------------

-=[ USER COMMANDS ]=-
	Can be edited or added to here: 
	...\Linux-Commander\Data\Linux Commander.json

	  ap:                           Linux shortcut for ansible command.
	                                Translates To: ansible-playbook
	                                e.g. ap [OPTIONS] [YAML_FILENAME]
	  ---------------------------------------------------------------------------
	  cd..:                         Corrects 'cd..' which will fail.
	                                Translates To: cd ..
	                                e.g. cd..
	  ---------------------------------------------------------------------------
	  cd/:                          Corrects 'cd/' which will fail.
	                                Translates To: cd /
	                                e.g. cd/
	  ---------------------------------------------------------------------------
	  co:                           Linux shortcut for change owner.
	                                Translates To: sudo chown
	                                e.g. co [USER]:[GROUP] [[DIRECTORY]|[FILE]]
	  ---------------------------------------------------------------------------
	  del:                          Windows command shortcut.
	                                Translates To: rm -f -r
	                                e.g. del * -=[Deletes all files and folders.
	                                e.g. del [FOLDER] -=[Delete a folder.
	                                e.g. del [FILENAME] -=[Delete a file.
	  ---------------------------------------------------------------------------
	  dir:                          Windows command shortcut.
	                                Translates To: ls -ltr
	                                e.g. dir -=[Uses default options above
	                                e.g. dir -altr -=[Uses only what you pass
	  ---------------------------------------------------------------------------
	  md:                           Windows command shortcut.
	                                Translates To: mkdir
	                                e.g. md [OPTIONS] [DIRECTORY_NAME]
	  ---------------------------------------------------------------------------
	  rd:                           Windows command shortcut.
	                                Translates To: rmdir
	                                e.g. rd /ME/SubFolder
	  ---------------------------------------------------------------------------
